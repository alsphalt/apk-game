namespace Sim.Core
{
    // Anything that can be added later as content (vehicle, part, city) has a unique id.
    public interface IContent
    {
        string Id { get; }
    }
}
