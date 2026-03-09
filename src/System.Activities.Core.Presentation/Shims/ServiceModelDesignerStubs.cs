using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Activities.Presentation.Metadata;

namespace System.ServiceModel.Activities.Presentation
{
    public sealed class ActivityXRefConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value;
    }

    public class BindingEditor : Control
    {
        public static readonly DependencyProperty BindingProperty = DependencyProperty.Register(nameof(Binding), typeof(object), typeof(BindingEditor));
        public object Binding
        {
            get => GetValue(BindingProperty);
            set => SetValue(BindingProperty, value);
        }
    }

    public class MessageQuerySetDesigner : Control
    {
        public static readonly DependencyProperty ActivityProperty = DependencyProperty.Register(nameof(Activity), typeof(object), typeof(MessageQuerySetDesigner));
        public static readonly DependencyProperty MessageQuerySetContainerProperty = DependencyProperty.Register(nameof(MessageQuerySetContainer), typeof(object), typeof(MessageQuerySetDesigner));
        public object Activity
        {
            get => GetValue(ActivityProperty);
            set => SetValue(ActivityProperty, value);
        }
        public object MessageQuerySetContainer
        {
            get => GetValue(MessageQuerySetContainerProperty);
            set => SetValue(MessageQuerySetContainerProperty, value);
        }
    }
}

namespace System.ServiceModel.Presentation
{
    public static class ServiceDesigner
    {
        public static void RegisterMetadata(AttributeTableBuilder builder)
        {
        }
    }
}
