using Microsoft.Xaml.Behaviors;
using Wpf.Ui.Controls;

namespace MultiLaunch.Behaviors
{
    public class PasswordBoxValueBehavior : Behavior<PasswordBox>
    {
        public static readonly DependencyProperty BoundPasswordProperty = DependencyProperty.Register(
            nameof(BoundPassword),
            typeof(string),
            typeof(PasswordBoxValueBehavior),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnBoundPasswordChanged
            )
        );

        public string BoundPassword
        {
            get => (string)GetValue(BoundPasswordProperty);
            set => SetValue(BoundPasswordProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.PasswordChanged += OnPasswordChanged;
            AssociatedObject.Password = BoundPassword;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.PasswordChanged -= OnPasswordChanged;

            base.OnDetaching();
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            BoundPassword = AssociatedObject.Password;
        }

        private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            PasswordBoxValueBehavior behavior = (PasswordBoxValueBehavior)d;

            if (behavior.AssociatedObject == null)
                return;

            string newPassword = (string)e.NewValue;

            if (behavior.AssociatedObject.Password != newPassword)
            {
                behavior.AssociatedObject.Password = newPassword;
            }
        }
    }
}