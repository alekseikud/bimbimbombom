using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AutomatonEditor
{

    public partial class MainWindow : Window
    {
        public Automaton automaton = new Automaton();
        public int stateCounter = 0;
        public State selectedState;
        public bool isDragging = false;
        private Point mouseOffset;

        public MainWindow()
        {
            InitializeComponent();
        }
        private void State_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Grid grid = (Grid)(sender);
            if (grid != null)
            {
                State selected = (State)(grid.Tag);
                foreach (var state in automaton.States)
                {
                    state.IsSelected = false;

                }
                selected.IsSelected = true;
                selectedState = selected;
                DrawStates();
                e.Handled = true;

            }

        }



        private void DrawStates()
        {
            MainCanvas.Children.Clear();
            foreach (var state in automaton.States)
            {
                Grid grid = new()
                {
                    Width = 50,
                    Height = 50

                };

                grid.Tag= state;
                grid.MouseLeftButtonDown += State_MouseLeftButtonDown;
                grid.MouseRightButtonDown += State_MouseRightButtonDown;
                grid.MouseMove += State_MouseMove;
                grid.MouseRightButtonUp += State_MouseRightButtonUp;


                Ellipse ellipse = new()
                {
                    Stroke = state.IsSelected?Brushes.Red : Brushes.Black,
                    Fill = Brushes.White,
                    StrokeThickness = 2
                };
                TextBlock text = new()
                {
                    Text = state.Name,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

     
                grid.Children.Add(ellipse);
                grid.Children.Add(text);

                Canvas.SetLeft(grid, state.X);
                Canvas.SetTop(grid, state.Y);
                MainCanvas.Children.Add(grid);
            }
        }

        private void State_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            Mouse.Capture(null);
            e.Handled = true;
        }

        private void State_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isDragging || selectedState==null)
            {
                return;
            }
            Grid grid = (Grid)(sender);

            Point mousePosition = e.GetPosition(MainCanvas);
            selectedState.X = mousePosition.X - mouseOffset.X;
            selectedState.Y = mousePosition.Y - mouseOffset.Y;
            DrawStates();

        }

        private void State_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Grid grid = (Grid)(sender);
            if (grid != null)
            {
                selectedState = (State)(grid.Tag);
                Point mousePosition = e.GetPosition(MainCanvas);
                mouseOffset = new Point(
                    mousePosition.X - selectedState.X,
                    mousePosition.Y - selectedState.Y
                );
                isDragging= true;
                grid.CaptureMouse();
                e.Handled = true;
            }
        }



        private void AddState_Click(object sender, RoutedEventArgs e)
        {
            State state = new()
            {
                Name = $"q{stateCounter}",
                X = 300,
                Y = 200
            };
            if(stateCounter++==0)
            {
                state.IsInitial = true;
            }
            automaton.States.Add(state);
            DrawStates();
        }

        private void MainCanvas_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            foreach (var state in automaton.States)
            {
                state.IsSelected = false;
            }
            selectedState = null;
            DrawStates();

        }
    }
}