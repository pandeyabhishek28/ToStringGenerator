namespace ToStringAnalyzer
{
    public class PropertyInfo
    {
        public string Name { get; }
        public bool IsValueType { get; }

        public PropertyInfo(string name, bool isValueType)
        {
            Name = name;
            IsValueType = isValueType;
        }

        public string GetPrintedValueForCSharp()
        {
            return IsValueType ? $"{{nameof({Name})}}={{{Name}.ToString()}}" : $"{{nameof({Name})}}={{{Name}}}";
        }
    }
}
