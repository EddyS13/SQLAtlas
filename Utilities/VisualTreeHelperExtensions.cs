// Utilities/VisualTreeHelperExtensions.cs

using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace SQLAtlas.Utilities
{
    public static class VisualTreeHelperExtensions
    {
        public static IEnumerable<T> FindVisualChildren<T>(this DependencyObject parent) where T : DependencyObject
        {
            if (parent != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                {
                    DependencyObject? child = VisualTreeHelper.GetChild(parent, i);
                    if (child is T tChild)
                    {
                        yield return tChild;
                    }

                    if (child != null)
                    {
                        foreach (T childOfChild in FindVisualChildren<T>(child))
                        {
                            yield return childOfChild;
                        }
                    }
                }
            }
        }
    }
}