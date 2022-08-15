using Unity.Entities;

[GenerateAuthoringComponent]
public struct Rioter : IComponentData
{
    public int policeRepulsion, threshhold;
    public float aggression;
}
