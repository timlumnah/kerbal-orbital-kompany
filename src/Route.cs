// Added namespace. Was in global namespace; unifies with rest of codebase.
namespace Koko
{
public class Route
{
    public string Id { get; set; }
    public string Origin { get; set; }
    public string Destination { get; set; }
    public string ResourceType { get; set; }
    public double Quantity { get; set; }
    public float FrequencyHours { get; set; }
    public double LastDeliveryTime { get; set; }


    public Route() { } // Required for deserialization

    public Route(string id, string origin, string destination, string resourceType, double quantity, float frequencyHours)
    {
        Id = id;
        Origin = origin;
        Destination = destination;
        ResourceType = resourceType;
        Quantity = quantity;
        FrequencyHours = frequencyHours;
        LastDeliveryTime = Planetarium.GetUniversalTime();
    }

    public Route(ConfigNode node)
    {
        Id = node.GetValue("Id");
        Origin = node.GetValue("Origin");
        Destination = node.GetValue("Destination");
        ResourceType = node.GetValue("ResourceType");
        Quantity = double.Parse(node.GetValue("Quantity"));
        FrequencyHours = float.Parse(node.GetValue("FrequencyHours"));
        LastDeliveryTime = node.HasValue("LastDeliveryTime") ? double.Parse(node.GetValue("LastDeliveryTime")) : Planetarium.GetUniversalTime();

    }

    public ConfigNode ToConfigNode()
    {
        ConfigNode node = new ConfigNode("ROUTE");
        node.AddValue("Id", Id);
        node.AddValue("Origin", Origin);
        node.AddValue("Destination", Destination);
        node.AddValue("ResourceType", ResourceType);
        node.AddValue("Quantity", Quantity);
        node.AddValue("FrequencyHours", FrequencyHours);
        node.AddValue("LastDeliveryTime", LastDeliveryTime);
        return node;
    }
} // class Route
} // namespace Koko
