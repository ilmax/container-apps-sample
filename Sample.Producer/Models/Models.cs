namespace Sample.Producer.Models;

public record Order(int Id, string Name, int Quantity, decimal Price);

public record Discount(decimal Amount);