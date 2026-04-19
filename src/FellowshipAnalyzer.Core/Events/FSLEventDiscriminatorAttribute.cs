namespace FellowshipAnalyzer.Core.Events;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public class FSLEventDiscriminatorAttribute(string typeDiscriminator) : Attribute
{
    public string TypeDiscriminator { get; set; } = typeDiscriminator;
}
