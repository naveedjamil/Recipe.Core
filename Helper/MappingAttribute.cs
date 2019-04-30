namespace Recipe.NetCore.Helper
{
    public class MappingAttribute : System.Attribute
    {
        public MappingAttribute(string name)
        {
            Name = name;
        }
        public string Name { get; }
    }
}
