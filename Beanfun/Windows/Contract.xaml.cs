using System.Windows;
using System.Windows.Input;

namespace Beanfun
{
    public partial class Contract : Window
    {
        public Contract(string ct)
        {
            InitializeComponent();
            contract.Text = ct;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }
    }
}
