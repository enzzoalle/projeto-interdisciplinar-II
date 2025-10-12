using System.Windows;

namespace ReplaysApp.Views
{
    public partial class InputBoxView : Window
    {
        public string ResponseText => InputTextBox.Text;

        public InputBoxView(string prompt, string defaultValue = "")
        {
            InitializeComponent();
            PromptLabel.Content = prompt;
            InputTextBox.Text = defaultValue;
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(InputTextBox.Text))
            {
                DialogResult = true;
            }
            else
            {
                MessageBox.Show("O nome não pode estar vazio.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}