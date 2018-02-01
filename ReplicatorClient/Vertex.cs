using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace ReplicatorClient
{
    public class Vertex : INotifyPropertyChanged
    {
        public Vertex()
        {
            color = new SolidColorBrush(Colors.Beige);
            color.Freeze();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        // This method is called by the Set accessor of each property. 
        // The CallerMemberName attribute that is applied to the optional propertyName 
        // parameter causes the property name of the caller to be substituted as an argument. 
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public double X
        {
            get
            {
                return this.x;
            }

            set
            {
                if (value != this.x)
                {
                    this.x = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public double Y
        {
            get
            {
                return this.y;
            }

            set
            {
                if (value != this.y)
                {
                    this.y = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public Brush C
        {
            get
            {
                return this.color;
            }

            set
            {
                if (value != this.color)
                {
                    this.color = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private double x = 0;
        private double y = 0;
        private Brush color;
    }
}
