namespace CompatibilityTests.Common
{
    public static class ExtensionMethods
    {
        public static T As<T>(this object target)
        {
            return (T) target;
        }
    }
}
