using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace AutomatonEditor
{
    public partial class MainWindow : Window
    {
        public Automaton automaton = new Automaton();
        public int stateCounter = 0;
        public State? selectedState;
        public Transition? selectedTransition;
        public bool isDragging = false;
        private Point mouseOffset;
        private Point dragStart;
        private bool wasDragged = false;
        private int simulationIndex = 0;
        private State? currentState;
        private Transition? activeTransition;
        private string activeSymbol = "";
        private bool isRunning = false;
        private readonly DispatcherTimer timer = new DispatcherTimer();

        public MainWindow()
        {
            InitializeComponent();
            timer.Tick += Timer_Tick;
            UpdateUi();
        }

        private void State_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Grid grid = (Grid)sender;
            SelectState((State)grid.Tag);
            e.Handled = true;
        }

        private void Transition_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Shape shape = (Shape)sender;
            SelectTransition((Transition)shape.Tag);
            e.Handled = true;
        }

        private void DrawStates()
        {
            MainCanvas.Children.Clear();
            DrawTransitions();

            foreach (var state in automaton.States)
            {
                double size = state.Radius * 2;
                Grid grid = new()
                {
                    Width = size,
                    Height = size,
                    Tag = state
                };

                grid.MouseLeftButtonDown += State_MouseLeftButtonDown;
                grid.MouseRightButtonDown += State_MouseRightButtonDown;
                grid.MouseMove += State_MouseMove;
                grid.MouseRightButtonUp += State_MouseRightButtonUp;

                Brush stroke = GetBrush(state.IsSelected || state == currentState ? "#CC0000" : state.StrokeColor);
                Ellipse ellipse = new()
                {
                    Stroke = stroke,
                    Fill = GetBrush(state.IsInitial ? "#BFE6FF" : state.FillColor),
                    StrokeThickness = state.StrokeThickness
                };
                TextBlock text = new()
                {
                    Text = state.Name,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                grid.Children.Add(ellipse);
                if (state.IsAccepting)
                {
                    Ellipse innerEllipse = new()
                    {
                        Stroke = stroke,
                        Fill = Brushes.Transparent,
                        StrokeThickness = state.StrokeThickness,
                        Margin = new Thickness(5)
                    };
                    grid.Children.Add(innerEllipse);
                }
                grid.Children.Add(text);
                grid.ContextMenu = CreateStateMenu(state);

                Canvas.SetLeft(grid, state.X);
                Canvas.SetTop(grid, state.Y);
                MainCanvas.Children.Add(grid);
            }
        }

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
                path.MouseLeftButtonDown += Transition_MouseLeftButtonDown;

                System.Windows.Shapes.Path hitPath = new()
                {
                    Data = geometry,
                    Stroke = Brushes.Transparent,
                    StrokeThickness = 14,
                    Tag = transition
                };
                hitPath.MouseLeftButtonDown += Transition_MouseLeftButtonDown;

                Polygon arrow = CreateArrow(transition, brush);
                arrow.Tag = transition;
                arrow.MouseLeftButtonDown += Transition_MouseLeftButtonDown;

                TextBlock label = new()
                {
                    Text = transition.Label,
                    Background = transition == activeTransition && transition.Label.Split(',').Any(symbol => symbol.Trim() == activeSymbol)
                        ? Brushes.LightYellow
                        : Brushes.White,
                    Foreground = brush,
                    Tag = transition
                };
                label.MouseLeftButtonDown += Transition_MouseLeftButtonDown;

                Point labelPoint = GetTransitionLabelPoint(transition);
                Canvas.SetLeft(label, labelPoint.X);
                Canvas.SetTop(label, labelPoint.Y);

                MainCanvas.Children.Add(path);
                MainCanvas.Children.Add(arrow);
                MainCanvas.Children.Add(label);
                MainCanvas.Children.Add(hitPath);
            }
        }

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

        private Point GetControlPoint(Transition transition)
        {
            Point start = GetStateCenter(transition.Source);
            Point end = GetStateCenter(transition.Target);
            double midX = (start.X + end.X) / 2;
            double midY = (start.Y + end.Y) / 2;
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);

            if (length == 0)
            {
                return new Point(midX, midY);
            }

            bool opposite = automaton.Transitions.Any(t => t.Source == transition.Target && t.Target == transition.Source);
            double offset = opposite ? 45 : 0;
            return new Point(midX - dy / length * offset, midY + dx / length * offset);
        }

        private Polygon CreateArrow(Transition transition, Brush brush)
        {
            Point end = GetStateCenter(transition.Target);
            Point control = transition.Source == transition.Target
                ? new Point(end.X + transition.Target.Radius, end.Y - transition.Target.Radius * 2)
                : GetControlPoint(transition);

            double dx = end.X - control.X;
            double dy = end.Y - control.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);

            if (length == 0)
            {
                length = 1;
            }

            double directionX = dx / length;
            double directionY = dy / length;
            Point arrowTip = new(end.X - directionX * transition.Target.Radius, end.Y - directionY * transition.Target.Radius);

            return new Polygon()
            {
                Fill = brush,
                Points = new PointCollection()
                {
                    arrowTip,
                    new Point(arrowTip.X - directionX * 12 - directionY * 6, arrowTip.Y - directionY * 12 + directionX * 6),
                    new Point(arrowTip.X - directionX * 12 + directionY * 6, arrowTip.Y - directionY * 12 - directionX * 6)
                }
            };
        }

        private Point GetTransitionLabelPoint(Transition transition)
        {
            Point start = GetStateCenter(transition.Source);
            Point end = GetStateCenter(transition.Target);

            if (transition.Source == transition.Target)
            {
                return new Point(start.X - 12, start.Y - transition.Source.Radius * 2.8);
            }

            Point control = GetControlPoint(transition);
            return new Point((start.X + 2 * control.X + end.X) / 4 - 12, (start.Y + 2 * control.Y + end.Y) / 4 - 12);
        }

        private Point GetStateCenter(State state)
        {
            return new Point(state.X + state.Radius, state.Y + state.Radius);
        }

        private void State_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            Mouse.Capture(null);
            if (!wasDragged)
            {
                Grid grid = (Grid)sender;
                grid.ContextMenu.IsOpen = true;
            }
            else
            {
                ResetSimulation();
                DrawStates();
            }
            e.Handled = true;
        }

        private void State_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isDragging || selectedState == null)
            {
                return;
            }

            Grid grid = (Grid)sender;
            Point mousePosition = e.GetPosition(MainCanvas);
            if (Math.Abs(mousePosition.X - dragStart.X) > 2 || Math.Abs(mousePosition.Y - dragStart.Y) > 2)
            {
                wasDragged = true;
            }
            selectedState.X = mousePosition.X - mouseOffset.X;
            selectedState.Y = mousePosition.Y - mouseOffset.Y;
            if (wasDragged)
            {
                DrawStates();
            }
            else
            {
                Canvas.SetLeft(grid, selectedState.X);
                Canvas.SetTop(grid, selectedState.Y);
            }
        }

        private void State_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            Grid grid = (Grid)sender;
            SelectState((State)grid.Tag);
            Point mousePosition = e.GetPosition(MainCanvas);
            dragStart = mousePosition;
            wasDragged = false;
            mouseOffset = new Point(mousePosition.X - selectedState!.X, mousePosition.Y - selectedState.Y);
            isDragging = true;
            grid.CaptureMouse();
            e.Handled = true;
        }

        private void AddState_Click(object sender, RoutedEventArgs e)
        {
            State state = new()
            {
                Name = $"q{stateCounter}",
                X = 300,
                Y = 200
            };
            if (stateCounter++ == 0)
            {
                state.IsInitial = true;
            }
            automaton.States.Add(state);
            SelectState(state);
            ResetSimulation();
        }

        private void MainCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ClearSelection();
            DrawStates();
            DrawTransitionsList();
        }

        private ContextMenu CreateStateMenu(State state)
        {
            ContextMenu menu = new();

            MenuItem acceptingItem = new()
            {
                Header = state.IsAccepting ? "Unmark as accepting" : "Mark as accepting"
            };
            acceptingItem.Click += (sender, e) =>
            {
                state.IsAccepting = !state.IsAccepting;
                ResetSimulation();
                DrawStates();
            };

            MenuItem initialItem = new()
            {
                Header = "Mark as initial"
            };
            initialItem.Click += (sender, e) =>
            {
                foreach (var automatonState in automaton.States)
                {
                    automatonState.IsInitial = false;
                }
                state.IsInitial = true;
                ResetSimulation();
                DrawStates();
            };

            MenuItem deleteItem = new()
            {
                Header = "Delete state"
            };
            deleteItem.Click += (sender, e) =>
            {
                RemoveTransitions(transition => transition.Source == state || transition.Target == state);
                automaton.States.Remove(state);
                if (selectedState == state)
                {
                    selectedState = null;
                }
                if (!automaton.States.Any(automatonState => automatonState.IsInitial) && automaton.States.Count > 0)
                {
                    automaton.States[0].IsInitial = true;
                }
                ResetSimulation();
                DrawStates();
                DrawTransitionsList();
                UpdateUi();
            };

            menu.Items.Add(acceptingItem);
            menu.Items.Add(initialItem);
            menu.Items.Add(deleteItem);

            return menu;
        }

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

        private void Transition_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;
            State endState = (State)checkBox.Tag;

            RemoveTransitions(transition => transition.Source == selectedState && transition.Target == endState);
            ResetSimulation();
            UpdateAlphabet();
            DrawStates();
        }

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

        private string GetLabelForTransitionRow(State endState)
        {
            foreach (var item in TransitionsList.Items)
            {
                if (item is StackPanel row && row.Children.OfType<TextBox>().FirstOrDefault() is TextBox textBox && textBox.Tag == endState)
                {
                    return textBox.Text;
                }
            }
            return "";
        }

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

        private void StateAppearance_Changed(object sender, RoutedEventArgs e)
        {
            if (MainCanvas == null)
            {
                return;
            }
            DrawStates();
        }

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

            Automaton result = new();
            Dictionary<int, State> statesById = new();
            foreach (var stateDto in dto.States)
            {
                if (string.IsNullOrWhiteSpace(stateDto.Name))
                {
                    throw new InvalidDataException("Every state must have a name.");
                }

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
                result.States.Add(state);
                statesById[stateDto.Id] = state;
            }

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

            return result;
        }

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
                }).ToList(),
                Transitions = automaton.Transitions
                    .SelectMany(transition => SplitSymbols(transition.Label).Select(symbol => new TransitionDto()
                    {
                        FromStateId = ids[transition.Source],
                        ToStateId = ids[transition.Target],
                        Symbol = symbol
                    }))
                    .ToList()
            };
        }

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

        private void InputWordBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ResetSimulation();
        }

        private void Previous_Click(object sender, RoutedEventArgs e)
        {
            if (simulationIndex == 0)
            {
                return;
            }

            simulationIndex--;
            RebuildSimulation();
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            StepForward();
        }

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

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            isRunning = false;
            timer.Stop();
            InputWordBox.IsEnabled = true;
            UpdateUi();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            ResetSimulation();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (!StepForward())
            {
                Stop_Click(this, new RoutedEventArgs());
            }
        }

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
            if (currentState == null)
            {
                return false;
            }

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

        private Transition? FindTransition(State source, string symbol)
        {
            return automaton.Transitions.FirstOrDefault(transition =>
                transition.Source == source && SplitSymbols(transition.Label).Contains(symbol));
        }

        private void ShowResult()
        {
            ResultText.Text = currentState?.IsAccepting == true ? "Accepted" : "Rejected";
        }

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

        private void UpdateWordPreview()
        {
            string word = InputWordBox.Text;
            if (word.Length == 0)
            {
                WordPreviewText.Text = "";
                return;
            }

            WordPreviewText.Text = string.Join(" ", word.Select((letter, index) => index == simulationIndex ? $"[{letter}]" : letter.ToString()));
        }

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

        private void SelectTransition(Transition transition)
        {
            ClearSelection();
            selectedTransition = transition;
            selectedTransition.IsSelected = true;
            DrawStates();
            DrawTransitionsList();
        }

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

        private void RemoveTransitions(Func<Transition, bool> condition)
        {
            foreach (var transition in automaton.Transitions.Where(condition).ToList())
            {
                automaton.Transitions.Remove(transition);
            }
        }

        private Brush GetBrush(string color)
        {
            try
            {
                return (Brush)new BrushConverter().ConvertFromString(color)!;
            }
            catch
            {
                return Brushes.White;
            }
        }
    }

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

    public class ColorToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (Brush)new BrushConverter().ConvertFromString(value?.ToString() ?? "#FFFFFF")!;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value.ToString() ?? "#FFFFFF";
        }
    }
}
