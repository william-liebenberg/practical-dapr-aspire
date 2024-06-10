namespace DaprShop.Contracts;

public record CartDto(string[] Items);

public record OrderDto(string[] Items);

public record ProductDto(string? Name, decimal UnitPrice);