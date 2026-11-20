using System.Numerics;

namespace DustysGanjaria;

// One order ticket: its number and the customer it belongs to. It hangs on the rail until it is dragged onto a station.
sealed class Ticket
{
    public int Number { get; }
    public Customer Customer { get; }
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
