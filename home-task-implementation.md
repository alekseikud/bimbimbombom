# Home Task Implementation Description

This document describes how the homework requirements were implemented in the WPF automaton editor. Each section follows one point from `home-task.md`: first the requirement is described, then the relevant implemented code is shown, then the behavior is explained.

The main files used by the implementation are:

- `Model.cs` - automaton, state, and transition data classes.
- `MainWindow.xaml` - UI layout.
- `MainWindow.xaml.cs` - drawing, editing, import/export, and simulation logic.

## 1. State Appearance Attributes

### Requirement

Each state has additional visual attributes:

- fill color,
- edge color,
- radius,
- edge thickness.

The UI allows changing those attributes for the active state using data binding and controls.

### Implemented Code

The state model was extended in `Model.cs`:

```csharp
public class State : INotifyPropertyChanged
{
    private double _x, _y;
    private double _radius = 25.0, _strokeThickness = 2.0;
    private bool _isInitial, _isAccepting, _isSelected;
    private string _fillColor = "#FFFFFF", _strokeColor = "#000000";

    public string? Name { get; set; }
    public double X { get => _x; set { _x = value; OnPropertyChanged(); } }
    public double Y { get => _y; set { _y = value; OnPropertyChanged(); } }
    public double Radius { get => _radius; set { _radius = value; OnPropertyChanged(); } }
    public double StrokeThickness { get => _strokeThickness; set { _strokeThickness = value; OnPropertyChanged(); } }
    public string FillColor { get => _fillColor; set { _fillColor = value; OnPropertyChanged(); } }
    public string StrokeColor { get => _strokeColor; set { _strokeColor = value; OnPropertyChanged(); } }
    public bool IsInitial { get => _isInitial; set { _isInitial = value; OnPropertyChanged(); } }
    public bool IsAccepting { get => _isAccepting; set { _isAccepting = value; OnPropertyChanged(); } }
    public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
}
```

The UI controls were added in `MainWindow.xaml`:

```xml
<StackPanel x:Name="StateEditorPanel" DataContext="{Binding SelectedState}">
    <TextBlock Text="Fill color"></TextBlock>
    <ComboBox x:Name="FillColorBox"
              SelectedValuePath="Content"
              SelectedValue="{Binding FillColor, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
              SelectionChanged="StateAppearance_Changed">
        <ComboBoxItem Content="#FFFFFF"></ComboBoxItem>
        <ComboBoxItem Content="#BFE6FF"></ComboBoxItem>
        <ComboBoxItem Content="#C8F7C5"></ComboBoxItem>
        <ComboBoxItem Content="#FFE8A3"></ComboBoxItem>
        <ComboBoxItem Content="#FFD1DC"></ComboBoxItem>
    </ComboBox>

    <TextBlock Text="Edge color" Margin="0,8,0,0"></TextBlock>
    <ComboBox x:Name="StrokeColorBox"
              SelectedValuePath="Content"
              SelectedValue="{Binding StrokeColor, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
              SelectionChanged="StateAppearance_Changed">
        <ComboBoxItem Content="#000000"></ComboBoxItem>
        <ComboBoxItem Content="#3366CC"></ComboBoxItem>
        <ComboBoxItem Content="#008000"></ComboBoxItem>
        <ComboBoxItem Content="#CC6600"></ComboBoxItem>
        <ComboBoxItem Content="#CC0000"></ComboBoxItem>
    </ComboBox>

    <TextBlock Text="Radius" Margin="0,8,0,0"></TextBlock>
    <Slider Minimum="18" Maximum="50"
            Value="{Binding Radius, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
            ValueChanged="StateAppearance_Changed"></Slider>

    <TextBlock Text="Edge thickness" Margin="0,8,0,0"></TextBlock>
    <Slider Minimum="1" Maximum="8"
            Value="{Binding StrokeThickness, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
            ValueChanged="StateAppearance_Changed"></Slider>
</StackPanel>
```

The selected state is connected to the editor panel in `MainWindow.xaml.cs`:

```csharp
private void SelectState(State state)
{
    ClearSelection();
    selectedState = state;
    selectedState.IsSelected = true;
    StateEditorPanel.DataContext = selectedState;
    DrawStates();
    DrawTransitionsList();
    UpdateUi();
}
```

The values are used while drawing:

```csharp
double size = state.Radius * 2;
Grid grid = new()
{
    Width = size,
    Height = size,
    Tag = state
};

Brush stroke = GetBrush(state.IsSelected || state == currentState ? "#CC0000" : state.StrokeColor);
Ellipse ellipse = new()
{
    Stroke = stroke,
    Fill = GetBrush(state.IsInitial ? "#BFE6FF" : state.FillColor),
    StrokeThickness = state.StrokeThickness
};
```

### Explanation

The visual attributes are stored directly in each `State`. Because the properties call `OnPropertyChanged`, the state class follows the same notification style as the starter model. The UI uses `ComboBox` controls for colors and `Slider` controls for numeric values. When a value changes, `StateAppearance_Changed` redraws the canvas.

## 2. Transition Labels, Arrow Ends, Self-Loops, Opposite Directions, and Alphabet

### Requirement

Each transition has a label like `a,b,c`. The user can specify the label while adding a transition. Transitions must have marked ends, self-loops must be allowed, opposite-direction transitions must not overlap, and the current alphabet must be displayed.

### Implemented Code

The transition model was extended in `Model.cs`:

```csharp
public class Transition : INotifyPropertyChanged
{
    private State _source = null!;
    private State _target = null!;
    private string _label = "";
    private bool _isSelected;

    public State Source { get => _source; set { _source = value; RefreshCoordinates(); } }
    public State Target { get => _target; set { _target = value; RefreshCoordinates(); } }
    public string Label { get => _label; set { _label = value; OnPropertyChanged(); } }
    public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
}
```

The transition list UI contains one checkbox and one label input for each possible target:

```csharp
private void DrawTransitionsList()
{
    TransitionsList.Items.Clear();
    if (selectedState == null)
    {
        return;
    }

    foreach (var state in automaton.States)
    {
        StackPanel row = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        Transition? transition = automaton.Transitions.FirstOrDefault(t => t.Source == selectedState && t.Target == state);

        CheckBox checkBox = new()
        {
            Content = state.Name,
            IsChecked = transition != null,
            Tag = state,
            Width = 70
        };

        TextBox labelBox = new()
        {
            Text = transition?.Label ?? "",
            Tag = state,
            Width = 120
        };

        checkBox.Checked += Transition_Checked;
        checkBox.Unchecked += Transition_Unchecked;
        labelBox.TextChanged += TransitionLabel_TextChanged;

        row.Children.Add(checkBox);
        row.Children.Add(labelBox);
        TransitionsList.Items.Add(row);
    }
}
```

Adding a transition creates a labeled `Transition` object:

```csharp
private void Transition_Checked(object sender, RoutedEventArgs e)
{
    CheckBox checkBox = (CheckBox)sender;
    State endState = (State)checkBox.Tag;
    string label = GetLabelForTransitionRow(endState);

    if (selectedState != null && !automaton.Transitions.Any(transition => transition.Source == selectedState && transition.Target == endState))
    {
        automaton.Transitions.Add(new Transition()
        {
            Source = selectedState,
            Target = endState,
            Label = string.IsNullOrWhiteSpace(label) ? "a" : label
        });
    }

    ResetSimulation();
    UpdateAlphabet();
    DrawStates();
    DrawTransitionsList();
}
```

Changing a label updates the existing transition:

```csharp
private void TransitionLabel_TextChanged(object sender, TextChangedEventArgs e)
{
    if (selectedState == null)
    {
        return;
    }

    TextBox textBox = (TextBox)sender;
    State endState = (State)textBox.Tag;
    Transition? transition = automaton.Transitions.FirstOrDefault(t => t.Source == selectedState && t.Target == endState);

    if (transition != null)
    {
        transition.Label = textBox.Text;
        ResetSimulation();
        UpdateAlphabet();
        DrawStates();
    }
}
```

Transitions are drawn as paths with arrowheads:

```csharp
private void DrawTransitions()
{
    foreach (var transition in automaton.Transitions)
    {
        PathGeometry geometry = CreateTransitionGeometry(transition);
        Brush brush = transition.IsSelected || transition == activeTransition ? Brushes.OrangeRed : Brushes.Black;

        System.Windows.Shapes.Path path = new()
        {
            Data = geometry,
            Stroke = brush,
            StrokeThickness = transition.IsSelected || transition == activeTransition ? 3 : 2,
            Tag = transition
        };

        Polygon arrow = CreateArrow(transition, brush);
        TextBlock label = new()
        {
            Text = transition.Label,
            Background = transition == activeTransition && transition.Label.Split(',').Any(symbol => symbol.Trim() == activeSymbol)
                ? Brushes.LightYellow
                : Brushes.White,
            Foreground = brush,
            Tag = transition
        };
    }
}
```

Self-loops and opposite-direction curves are handled by geometry helpers:

```csharp
private PathGeometry CreateTransitionGeometry(Transition transition)
{
    Point start = GetStateCenter(transition.Source);
    Point end = GetStateCenter(transition.Target);
    PathFigure figure = new() { StartPoint = start };

    if (transition.Source == transition.Target)
    {
        double r = transition.Source.Radius;
        Point p1 = new(start.X - r, start.Y - r * 2.2);
        Point p2 = new(start.X + r, start.Y - r * 2.2);
        figure.Segments.Add(new BezierSegment(p1, p2, end, true));
    }
    else
    {
        Point control = GetControlPoint(transition);
        figure.Segments.Add(new QuadraticBezierSegment(control, end, true));
    }

    return new PathGeometry(new[] { figure });
}
```

```csharp
private Point GetControlPoint(Transition transition)
{
    Point start = GetStateCenter(transition.Source);
    Point end = GetStateCenter(transition.Target);
    double dx = end.X - start.X;
    double dy = end.Y - start.Y;
    double length = Math.Sqrt(dx * dx + dy * dy);

    bool opposite = automaton.Transitions.Any(t => t.Source == transition.Target && t.Target == transition.Source);
    double offset = opposite ? 45 : 0;
    return new Point(midX - dy / length * offset, midY + dx / length * offset);
}
```

The alphabet is extracted from all labels:

```csharp
private void UpdateAlphabet()
{
    AlphabetText.Text = "{ " + string.Join(", ", GetAlphabet()) + " }";
}

private IEnumerable<string> GetAlphabet()
{
    return automaton.Transitions
        .SelectMany(transition => SplitSymbols(transition.Label))
        .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
        .Distinct()
        .OrderBy(symbol => symbol);
}

private IEnumerable<string> SplitSymbols(string label)
{
    return label.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
```

### Explanation

A transition stores all accepted symbols in one label string, for example `0,1`. The alphabet is computed automatically by splitting all labels by commas. Transitions are drawn with WPF `PathGeometry`, which makes it possible to draw normal curves, opposite-direction curves, and self-loops.

## 3. Deleting a Transition

### Requirement

A transition can be activated like a state, and the active transition can be deleted.

### Implemented Code

The transition drawing objects are clickable:

```csharp
private void Transition_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    Shape shape = (Shape)sender;
    SelectTransition((Transition)shape.Tag);
    e.Handled = true;
}
```

Selecting a transition marks it as active:

```csharp
private void SelectTransition(Transition transition)
{
    ClearSelection();
    selectedTransition = transition;
    selectedTransition.IsSelected = true;
    DrawStates();
    DrawTransitionsList();
}
```

The toolbar contains a delete button:

```xml
<Button Content="Delete Transition"
        Width="115"
        Margin="0,0,15,0"
        Click="DeleteTransition_Click"></Button>
```

The selected transition is removed:

```csharp
private void DeleteTransition_Click(object sender, RoutedEventArgs e)
{
    if (selectedTransition == null)
    {
        return;
    }

    automaton.Transitions.Remove(selectedTransition);
    selectedTransition = null;
    ResetSimulation();
    UpdateAlphabet();
    DrawStates();
    DrawTransitionsList();
}
```

### Explanation

When the user clicks a transition line, arrow, or label, that transition becomes selected and is highlighted. Pressing `Delete Transition` removes it from `automaton.Transitions`, refreshes the alphabet, and redraws the canvas.

## 4. Import from JSON File

### Requirement

The user can import an automaton from a JSON file. The file format should match the provided `automaton.json`. Invalid data should display an error message.

### Implemented Code

The import button is in `MainWindow.xaml`:

```xml
<Button Content="Import" Width="80" Margin="0,0,5,0" Click="Import_Click"></Button>
```

The import handler opens a file dialog, validates the path, loads the JSON, and catches errors:

```csharp
private void Import_Click(object sender, RoutedEventArgs e)
{
    try
    {
        OpenFileDialog dialog = new()
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = GetImportDirectory()
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        Automaton imported = LoadAutomaton(dialog.FileName);
        automaton = imported;
        stateCounter = automaton.States.Count;
        selectedState = null;
        selectedTransition = null;
        ResetSimulation();
        DrawStates();
        DrawTransitionsList();
        UpdateAlphabet();
        UpdateUi();
    }
    catch (Exception exception)
    {
        MessageBox.Show(exception.Message, "Invalid automaton", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
```

The default folder is resolved safely:

```csharp
private string GetImportDirectory()
{
    string exampleDirectory = System.IO.Path.GetFullPath(
        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "WPF2"));

    if (Directory.Exists(exampleDirectory))
    {
        return exampleDirectory;
    }

    return AppDomain.CurrentDomain.BaseDirectory;
}
```

JSON is deserialized into DTO classes and validated:

```csharp
private Automaton LoadAutomaton(string path)
{
    AutomatonDto? dto = JsonSerializer.Deserialize<AutomatonDto>(File.ReadAllText(path), new JsonSerializerOptions()
    {
        PropertyNameCaseInsensitive = true
    });

    if (dto == null || dto.States == null || dto.States.Count == 0)
    {
        throw new InvalidDataException("The file does not contain states.");
    }

    if (dto.States.Count(state => state.IsStart) != 1)
    {
        throw new InvalidDataException("The automaton must have exactly one initial state.");
    }
}
```

States are created from the imported data:

```csharp
State state = new()
{
    Name = stateDto.Name,
    X = stateDto.Position?.X ?? 100,
    Y = stateDto.Position?.Y ?? 100,
    IsInitial = stateDto.IsStart,
    IsAccepting = stateDto.IsAccepting,
    Radius = stateDto.Appearance?.Radius ?? 25,
    FillColor = stateDto.Appearance?.FillColor ?? "#FFFFFF",
    StrokeColor = stateDto.Appearance?.StrokeColor ?? "#000000",
    StrokeThickness = stateDto.Appearance?.StrokeThickness ?? 2
};
```

Transitions are created from imported `fromStateId`, `toStateId`, and `symbol` values:

```csharp
foreach (var transitionDto in dto.Transitions ?? [])
{
    if (!statesById.TryGetValue(transitionDto.FromStateId, out State? source) ||
        !statesById.TryGetValue(transitionDto.ToStateId, out State? target))
    {
        throw new InvalidDataException("A transition refers to a missing state.");
    }

    Transition? transition = result.Transitions.FirstOrDefault(t => t.Source == source && t.Target == target);
    if (transition == null)
    {
        result.Transitions.Add(new Transition()
        {
            Source = source,
            Target = target,
            Label = transitionDto.Symbol ?? ""
        });
    }
    else if (!string.IsNullOrWhiteSpace(transitionDto.Symbol))
    {
        transition.Label = string.IsNullOrWhiteSpace(transition.Label)
            ? transitionDto.Symbol
            : $"{transition.Label},{transitionDto.Symbol}";
    }
}
```

### Explanation

The importer matches the sample JSON structure. The sample format stores one transition per symbol, while the editor stores one transition per pair of states with a comma-separated label. During import, transitions with the same source and target are merged into one label.

## 5. Export to JSON File

### Requirement

The user can export a valid automaton to JSON using the same format as the provided example.

### Implemented Code

The export button is in `MainWindow.xaml`:

```xml
<Button Content="Export JSON" Width="90" Margin="0,0,5,0" Click="ExportJson_Click"></Button>
```

The export handler validates the automaton and writes JSON:

```csharp
private void ExportJson_Click(object sender, RoutedEventArgs e)
{
    string? error = ValidateAutomaton();
    if (error != null)
    {
        MessageBox.Show(error, "Invalid automaton", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
    }

    SaveFileDialog dialog = new()
    {
        Filter = "JSON files (*.json)|*.json",
        FileName = "automaton.json"
    };

    if (dialog.ShowDialog() == true)
    {
        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(CreateDto(), new JsonSerializerOptions()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }
}
```

The DTO is built from current states and transitions:

```csharp
private AutomatonDto CreateDto()
{
    Dictionary<State, int> ids = automaton.States.Select((state, index) => new { state, index })
        .ToDictionary(item => item.state, item => item.index);

    return new AutomatonDto()
    {
        Meta = new MetaDto()
        {
            Description = "Exported automaton",
            Alphabet = GetAlphabet().ToList(),
            Created = DateTime.UtcNow
        },
        States = automaton.States.Select(state => new StateDto()
        {
            Id = ids[state],
            Name = state.Name ?? "",
            IsStart = state.IsInitial,
            IsAccepting = state.IsAccepting,
            Position = new PositionDto() { X = state.X, Y = state.Y },
            Appearance = new AppearanceDto()
            {
                Radius = state.Radius,
                FillColor = state.FillColor,
                StrokeColor = state.StrokeColor,
                StrokeThickness = state.StrokeThickness
            }
        }).ToList()
    };
}
```

Transition labels are split into separate JSON transition entries:

```csharp
Transitions = automaton.Transitions
    .SelectMany(transition => SplitSymbols(transition.Label).Select(symbol => new TransitionDto()
    {
        FromStateId = ids[transition.Source],
        ToStateId = ids[transition.Target],
        Symbol = symbol
    }))
    .ToList()
```

Validation checks basic DFA structure:

```csharp
private string? ValidateAutomaton()
{
    if (automaton.States.Count == 0)
    {
        return "The automaton must contain at least one state.";
    }
    if (automaton.States.Count(state => state.IsInitial) != 1)
    {
        return "The automaton must have exactly one initial state.";
    }
    if (automaton.Transitions.Any(transition => !SplitSymbols(transition.Label).Any()))
    {
        return "Every transition must have a label.";
    }
    return null;
}
```

### Explanation

Export converts the internal model back into the required JSON shape. Because internal labels can contain multiple symbols, each symbol is exported as a separate transition entry. This keeps the output compatible with `automaton.json`.

## 6. Export to Image

### Requirement

The user can export the automaton as an image.

### Implemented Code

The PNG export button is in `MainWindow.xaml`:

```xml
<Button Content="Export PNG" Width="90" Margin="0,0,15,0" Click="ExportPng_Click"></Button>
```

The canvas is rendered to a PNG file:

```csharp
private void ExportPng_Click(object sender, RoutedEventArgs e)
{
    SaveFileDialog dialog = new()
    {
        Filter = "PNG image (*.png)|*.png",
        FileName = "automaton.png"
    };

    if (dialog.ShowDialog() != true)
    {
        return;
    }

    Size size = new(MainCanvas.ActualWidth, MainCanvas.ActualHeight);
    MainCanvas.Measure(size);
    MainCanvas.Arrange(new Rect(size));
    RenderTargetBitmap bitmap = new((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
    bitmap.Render(MainCanvas);

    PngBitmapEncoder encoder = new();
    encoder.Frames.Add(BitmapFrame.Create(bitmap));
    using FileStream stream = File.Create(dialog.FileName);
    encoder.Save(stream);
}
```

### Explanation

The current WPF canvas is rendered into a `RenderTargetBitmap`. A `PngBitmapEncoder` then writes the bitmap to disk as a PNG image.

## 7. Entering and Validating an Input Word

### Requirement

The runtime environment allows entering an input word. The word must only contain symbols from the current alphabet. During computation the current symbol is highlighted, and the word cannot be edited while the animation is running.

### Implemented Code

The runtime input controls were added in `MainWindow.xaml`:

```xml
<TextBlock Text="Input word"></TextBlock>
<TextBox x:Name="InputWordBox" TextChanged="InputWordBox_TextChanged"></TextBox>
<TextBlock x:Name="WordPreviewText" FontSize="16" Margin="0,6,0,6"></TextBlock>
```

Changing the input resets the simulation:

```csharp
private void InputWordBox_TextChanged(object sender, TextChangedEventArgs e)
{
    ResetSimulation();
}
```

The input word is validated against the alphabet:

```csharp
private bool ValidateWord()
{
    string? error = ValidateAutomaton();
    if (error != null)
    {
        ResultText.Text = error;
        return false;
    }

    HashSet<string> alphabet = GetAlphabet().ToHashSet();
    foreach (char letter in InputWordBox.Text)
    {
        if (!alphabet.Contains(letter.ToString()))
        {
            ResultText.Text = $"Invalid symbol: {letter}";
            return false;
        }
    }

    return true;
}
```

The current symbol is shown with brackets:

```csharp
private void UpdateWordPreview()
{
    string word = InputWordBox.Text;
    if (word.Length == 0)
    {
        WordPreviewText.Text = "";
        return;
    }

    WordPreviewText.Text = string.Join(" ", word.Select((letter, index) =>
        index == simulationIndex ? $"[{letter}]" : letter.ToString()));
}
```

Editing is disabled during animation:

```csharp
private void Start_Click(object sender, RoutedEventArgs e)
{
    if (!ValidateWord())
    {
        return;
    }

    isRunning = true;
    InputWordBox.IsEnabled = false;
    timer.Interval = TimeSpan.FromMilliseconds(2200 - SpeedSlider.Value);
    timer.Start();
    UpdateUi();
}
```

### Explanation

The alphabet is not typed manually. It is extracted from transition labels. If the input contains a character that is not in that alphabet, the simulation does not start and an error is displayed.

## 8. Step-by-Step Mode

### Requirement

The `Next` and `Previous` buttons move the computation forward and backward. Buttons are enabled or disabled depending on the current position. The current state, active transition, and active symbol are highlighted. At the end, the user sees whether the word is accepted or rejected.

### Implemented Code

The step controls are in `MainWindow.xaml`:

```xml
<StackPanel Orientation="Horizontal">
    <Button x:Name="PreviousButton" Content="Previous" Width="78" Margin="0,0,5,0" Click="Previous_Click"></Button>
    <Button x:Name="NextButton" Content="Next" Width="78" Click="Next_Click"></Button>
</StackPanel>
```

The next step is handled by `StepForward`:

```csharp
private bool StepForward()
{
    if (!ValidateWord())
    {
        return false;
    }

    string word = InputWordBox.Text;
    if (simulationIndex >= word.Length)
    {
        ShowResult();
        return false;
    }

    currentState ??= automaton.States.FirstOrDefault(state => state.IsInitial);
    activeSymbol = word[simulationIndex].ToString();
    activeTransition = FindTransition(currentState, activeSymbol);
    HistoryList.Items.Add($"{currentState.Name}, {activeSymbol}");
    simulationIndex++;

    if (activeTransition == null)
    {
        ResultText.Text = "Rejected";
        isRunning = false;
        timer.Stop();
        InputWordBox.IsEnabled = true;
        DrawStates();
        UpdateWordPreview();
        UpdateUi();
        return false;
    }

    currentState = activeTransition.Target;
    if (simulationIndex == word.Length)
    {
        ShowResult();
    }

    DrawStates();
    UpdateWordPreview();
    UpdateUi();
    return simulationIndex < word.Length;
}
```

The previous step rebuilds the simulation up to the previous index:

```csharp
private void Previous_Click(object sender, RoutedEventArgs e)
{
    if (simulationIndex == 0)
    {
        return;
    }

    simulationIndex--;
    RebuildSimulation();
}
```

```csharp
private void RebuildSimulation()
{
    string word = InputWordBox.Text;
    HistoryList.Items.Clear();
    currentState = automaton.States.FirstOrDefault(state => state.IsInitial);
    activeTransition = null;
    activeSymbol = "";
    ResultText.Text = "";

    int targetIndex = simulationIndex;
    simulationIndex = 0;
    for (int i = 0; i < targetIndex; i++)
    {
        StepForward();
    }
    simulationIndex = Math.Min(targetIndex, word.Length);
    UpdateWordPreview();
    DrawStates();
    UpdateUi();
}
```

The result is displayed after the last symbol:

```csharp
private void ShowResult()
{
    ResultText.Text = currentState?.IsAccepting == true ? "Accepted" : "Rejected";
}
```

Buttons are enabled or disabled in `UpdateUi`:

```csharp
private void UpdateUi()
{
    bool hasAutomaton = automaton.States.Any();
    PreviousButton.IsEnabled = hasAutomaton && simulationIndex > 0 && !isRunning;
    NextButton.IsEnabled = hasAutomaton && simulationIndex < InputWordBox.Text.Length && !isRunning;
    StartButton.IsEnabled = hasAutomaton && !isRunning;
    StopButton.IsEnabled = isRunning;
    ResetButton.IsEnabled = hasAutomaton;
    StateEditorPanel.IsEnabled = selectedState != null;
}
```

### Explanation

The simulation stores the current state and current index in the input word. `Next` consumes one symbol and follows the matching transition. `Previous` rebuilds the computation from the start until the previous position, which keeps the logic simple and reliable.

## 9. Animation Mode

### Requirement

The runtime has `Start`, `Stop`, and `Reset` buttons. The animation advances automatically and the speed is controlled by a slider.

### Implemented Code

The animation controls were added in `MainWindow.xaml`:

```xml
<StackPanel Orientation="Horizontal" Margin="0,6,0,0">
    <Button x:Name="StartButton" Content="Start" Width="58" Margin="0,0,5,0" Click="Start_Click"></Button>
    <Button x:Name="StopButton" Content="Stop" Width="58" Margin="0,0,5,0" Click="Stop_Click"></Button>
    <Button x:Name="ResetButton" Content="Reset" Width="58" Click="Reset_Click"></Button>
</StackPanel>
<TextBlock Text="Speed" Margin="0,8,0,0"></TextBlock>
<Slider x:Name="SpeedSlider" Minimum="200" Maximum="2000" Value="800"></Slider>
```

The timer is initialized in the constructor:

```csharp
private readonly DispatcherTimer timer = new DispatcherTimer();

public MainWindow()
{
    InitializeComponent();
    timer.Tick += Timer_Tick;
    UpdateUi();
}
```

Starting the animation:

```csharp
private void Start_Click(object sender, RoutedEventArgs e)
{
    if (!ValidateWord())
    {
        return;
    }

    isRunning = true;
    InputWordBox.IsEnabled = false;
    timer.Interval = TimeSpan.FromMilliseconds(2200 - SpeedSlider.Value);
    timer.Start();
    UpdateUi();
}
```

Stopping the animation:

```csharp
private void Stop_Click(object sender, RoutedEventArgs e)
{
    isRunning = false;
    timer.Stop();
    InputWordBox.IsEnabled = true;
    UpdateUi();
}
```

Resetting the simulation:

```csharp
private void Reset_Click(object sender, RoutedEventArgs e)
{
    ResetSimulation();
}
```

The timer advances by one simulation step:

```csharp
private void Timer_Tick(object? sender, EventArgs e)
{
    if (!StepForward())
    {
        Stop_Click(this, new RoutedEventArgs());
    }
}
```

### Explanation

Animation mode uses `DispatcherTimer`, which is natural for WPF because it runs on the UI thread. Every timer tick calls the same `StepForward` method used by the `Next` button, so step mode and animation mode share the same simulation logic.

## 10. State History

### Requirement

The UI displays computation history as a list of pairs containing the current state and processed letter. Loading an automaton or changing the input word resets the history. `Next` adds an item, and `Previous` removes the last item.

### Implemented Code

The history list was added in `MainWindow.xaml`:

```xml
<TextBlock Text="History" FontWeight="Bold"></TextBlock>
<ListBox x:Name="HistoryList" MinHeight="120"></ListBox>
```

`StepForward` adds one history item:

```csharp
activeSymbol = word[simulationIndex].ToString();
activeTransition = FindTransition(currentState, activeSymbol);
HistoryList.Items.Add($"{currentState.Name}, {activeSymbol}");
simulationIndex++;
```

`ResetSimulation` clears the history:

```csharp
private void ResetSimulation()
{
    isRunning = false;
    timer.Stop();
    simulationIndex = 0;
    currentState = automaton.States.FirstOrDefault(state => state.IsInitial);
    activeTransition = null;
    activeSymbol = "";
    HistoryList.Items.Clear();
    ResultText.Text = "";
    InputWordBox.IsEnabled = true;
    UpdateAlphabet();
    UpdateWordPreview();
    UpdateUi();
    DrawStates();
}
```

`RebuildSimulation` clears and rebuilds history when going backward:

```csharp
private void RebuildSimulation()
{
    HistoryList.Items.Clear();
    currentState = automaton.States.FirstOrDefault(state => state.IsInitial);
    activeTransition = null;
    activeSymbol = "";
    ResultText.Text = "";

    int targetIndex = simulationIndex;
    simulationIndex = 0;
    for (int i = 0; i < targetIndex; i++)
    {
        StepForward();
    }
}
```

### Explanation

The history list is updated by the same code that processes symbols. This means the displayed history always matches the actual computation. When the input word changes, the automaton is imported, or the user resets the simulation, the history is cleared.

## 11. JSON DTO Classes

### Requirement

The imported and exported JSON must be compatible with the example file. The example contains `meta`, `states`, `position`, `appearance`, and `transitions`.

### Implemented Code

The DTO classes are defined in `MainWindow.xaml.cs`:

```csharp
public class AutomatonDto
{
    public MetaDto? Meta { get; set; }
    public List<StateDto>? States { get; set; }
    public List<TransitionDto>? Transitions { get; set; }
}

public class MetaDto
{
    public string? Description { get; set; }
    public List<string> Alphabet { get; set; } = [];
    public DateTime Created { get; set; }
}

public class StateDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public bool IsStart { get; set; }
    public bool IsAccepting { get; set; }
    public PositionDto? Position { get; set; }
    public AppearanceDto? Appearance { get; set; }
}

public class PositionDto
{
    public double X { get; set; }
    public double Y { get; set; }
}

public class AppearanceDto
{
    public double Radius { get; set; }
    public string FillColor { get; set; } = "#FFFFFF";
    public string StrokeColor { get; set; } = "#000000";
    public double StrokeThickness { get; set; } = 2.0;
}

public class TransitionDto
{
    public int FromStateId { get; set; }
    public int ToStateId { get; set; }
    public string? Symbol { get; set; }
}
```

### Explanation

DTO classes separate the JSON format from the internal editor model. This makes import and export easier because the code can map between the sample JSON shape and the application's object model.

## 12. Selection and Highlighting

### Requirement

The active state or transition should be visible. During simulation, the current state and active transition should be highlighted.

### Implemented Code

State selection:

```csharp
private void SelectState(State state)
{
    ClearSelection();
    selectedState = state;
    selectedState.IsSelected = true;
    StateEditorPanel.DataContext = selectedState;
    DrawStates();
    DrawTransitionsList();
    UpdateUi();
}
```

Transition selection:

```csharp
private void SelectTransition(Transition transition)
{
    ClearSelection();
    selectedTransition = transition;
    selectedTransition.IsSelected = true;
    DrawStates();
    DrawTransitionsList();
}
```

Clearing selection:

```csharp
private void ClearSelection()
{
    foreach (var state in automaton.States)
    {
        state.IsSelected = false;
    }
    foreach (var transition in automaton.Transitions)
    {
        transition.IsSelected = false;
    }
    selectedState = null;
    selectedTransition = null;
    StateEditorPanel.DataContext = null;
    UpdateUi();
}
```

Drawing highlights:

```csharp
Brush stroke = GetBrush(state.IsSelected || state == currentState ? "#CC0000" : state.StrokeColor);
```

```csharp
Brush brush = transition.IsSelected || transition == activeTransition ? Brushes.OrangeRed : Brushes.Black;
```

### Explanation

Selection is stored directly in the model with `IsSelected`. Runtime highlighting compares the drawn state or transition with `currentState` and `activeTransition`.

## 13. Summary of Implemented Features

The final implementation includes:

- state creation, selection, dragging, initial marking, accepting marking, and deletion,
- state appearance editing,
- transition creation with labels,
- self-loop and opposite-direction transition drawing,
- transition selection and deletion,
- alphabet extraction from transition labels,
- JSON import and validation,
- JSON export,
- PNG export,
- input word validation,
- step-by-step simulation,
- animation mode,
- current symbol/state/transition highlighting,
- computation history.

The implementation keeps the original simple style: the canvas is redrawn manually from the `Automaton` model, and most behavior is kept in `MainWindow.xaml.cs` so it is easy to follow for the assignment.
