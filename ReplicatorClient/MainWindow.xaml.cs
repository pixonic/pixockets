using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Point = System.Windows.Point;

namespace ReplicatorClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //UI
        private Point StartPoint;
        private double OriginalLeft;
        private double OriginalTop;
        private bool IsDown;
        private bool IsDragging;
        private Shape FollowerView;
        private Vertex FollowerModel;
        private Random Rnd = new Random(Guid.NewGuid().GetHashCode());

        //Network
        private Sync Replicator = new Sync();
        private MultiDict<int, Vertex> Vertices = new MultiDict<int, Vertex>();

        public MainWindow()
        {
            ConsoleManager.Show();

            InitializeComponent();

            Replicator.OnNewFollower += AddFollower;
            Replicator.OnDeleteFollower += RemoveFollower;

            var x = (float)(Rnd.NextDouble() * (Width - 64));
            var y = (float)(Rnd.NextDouble() * (Height - 64));
            Replicator.Start(x, y);
        }

        private static void BindVertex(FrameworkElement view, Vertex model)
        {
            Binding cBinding = new Binding("C");
            cBinding.Source = model;
            view.SetBinding(Shape.FillProperty, cBinding);
            Binding xBinding = new Binding("X");
            xBinding.Source = model;
            view.SetBinding(Canvas.LeftProperty, xBinding);
            Binding yBinding = new Binding("Y");
            yBinding.Source = model;
            view.SetBinding(Canvas.TopProperty, yBinding);
        }

        private Shape CreateVertexView(int id)
        {
            Ellipse vertView = new Ellipse();
            vertView.Width = 64;
            vertView.Height = 64;

            vertView.Stroke = Brushes.Black;

            vertView.Fill = Brushes.Beige;
            vertView.StrokeThickness = 3;
            vertView.Name = IdToName(id);

            Scene.Children.Add(vertView);

            return vertView;
        }


        private void AddFollower(int id)
        {
            var v = new Vertex();
            Vertices.Add(id, v);

            Replicator.AddObject(id, v);

            Dispatcher.Invoke((() =>
            {
                FrameworkElement elem = CreateVertexView(id);

                BindVertex(elem, v);
            }));
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
        }

        private void RemoveFollower(int id)
        {
            //We have never had this element
            if (!Vertices.ContainsKey(id))
            {
                return;
            }

            Vertices.Remove(id);

            this.Dispatcher.Invoke((Action)(() =>
            {
                FrameworkElement followerView = IdToElement(id);
                Scene.Children.Remove(followerView);
            }));
        }

        private void Scene_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
        }

        private void Scene_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Source == Scene)
            {
                return;
            }

            IsDown = true;
            StartPoint = e.GetPosition(Scene);

            FollowerView = e.Source as Shape;
            int vId = ElementNameToId(FollowerView);

            if (vId != Replicator.MyId)
            {
                return;
            }

            e.Handled = true;

            FollowerModel = Vertices.Get(vId);
            Scene.CaptureMouse();
        }

        private void Scene_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
        }

        private void Scene_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (IsDown)
            {
                if ((IsDragging == false) && (FollowerModel != null) &&
                    ((Math.Abs(e.GetPosition(Scene).X - StartPoint.X) > SystemParameters.MinimumHorizontalDragDistance) ||
                    (Math.Abs(e.GetPosition(Scene).Y - StartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)))
                {
                    DragStarted();
                }
                if (IsDragging)
                {
                    DragMoved();
                }
            }
        }

        private void Scene_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (IsDown)
            {
                DragFinished();
                e.Handled = true;
            }
        }

        private void DragStarted()
        {
            IsDragging = true;
            OriginalLeft = Canvas.GetLeft(FollowerView);
            OriginalTop = Canvas.GetTop(FollowerView);
        }

        private void DragMoved()
        {
            Point CurrentPosition = Mouse.GetPosition(Scene);

            double x = OriginalLeft + CurrentPosition.X - StartPoint.X;
            double y = OriginalTop + CurrentPosition.Y - StartPoint.Y;

            FollowerModel.X = x;
            FollowerModel.Y = y;
        }

        private void DragFinished()
        {
            Mouse.Capture(null);

            IsDragging = false;
            IsDown = false;
        }

        private string IdToName(int id)
        {
            return "_" + id.ToString();
        }

        private int ElementNameToId(FrameworkElement elem)
        {
            return int.Parse(elem.Name.Substring(1));
        }

        private FrameworkElement IdToElement(int id)
        {
            string name = IdToName(id);

            foreach (FrameworkElement elem in Scene.Children)
            {
                if (elem.Name == name)
                {
                    return elem;
                }
            }

            return null;
        }
    }
}
