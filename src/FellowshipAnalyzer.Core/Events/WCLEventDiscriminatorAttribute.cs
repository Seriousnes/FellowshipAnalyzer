namespace FellowshipAnalyzer.Core.Events;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public class WCLEventDiscriminatorAttribute(string typeDiscriminator) : Attribute
{
    public string TypeDiscriminator { get; set; } = typeDiscriminator;
}
