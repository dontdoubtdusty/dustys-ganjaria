using System.Numerics;

namespace DustysGanjaria;

// The station an order needs next.
enum Stage { Weigh, Pack, Label }

// One order ticket: its number and the customer it belongs to. It hangs on the rail until it is dragged onto the
// station it needs next, and goes back up on the rail between stations.
sealed class Ticket
{
    public int Number { get; }
    public Customer Customer { get; }
    // The station this order needs next.
    public Stage Next = Stage.Weigh;
    // The weigh station's result, once the bag is weighed, sealed and in the OUT bin.
    public WeighResult? Weighed;
    // The pack station's result, once the container is closed (its Used container goes on to Label).
    public PackResult? Packed;
    // Where the card is drawn on the rail; it eases toward its slot.
    public Vector2 Pos;
    // Seconds left of the glow a new ticket gets when it goes up on the rail.
    public float Fresh;

    public Ticket(int number, Customer customer)
    {
        Number = number;
        Customer = customer;
    }

    // The number as printed on the ticket, like #007.
    public string Label => $"#{Number:000}";
}
