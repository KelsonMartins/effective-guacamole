namespace Guacamole.ObjectMapper.Tests;

// ---------- plain POCOs used across all test classes ----------

public enum Status { Active, Inactive, Pending }

public class Source
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public decimal Balance { get; set; }
    public Status Status { get; set; }
    public Address? Address { get; set; }
    public List<Tag> Tags { get; set; } = [];
}

public class Destination
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public decimal Balance { get; set; }
    public Status Status { get; set; }
    public AddressDto? Address { get; set; }
    public List<TagDto> Tags { get; set; } = [];
}

public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

public class AddressDto
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

public class Tag
{
    public string Value { get; set; } = string.Empty;
}

public class TagDto
{
    public string Value { get; set; } = string.Empty;
}

// For circular reference tests
public class Parent
{
    public string Name { get; set; } = string.Empty;
    public List<Child> Children { get; set; } = [];
}

public class Child
{
    public string Name { get; set; } = string.Empty;
    public Parent? Parent { get; set; }
}

public class ParentDto
{
    public string Name { get; set; } = string.Empty;
    public List<ChildDto> Children { get; set; } = [];
}

public class ChildDto
{
    public string Name { get; set; } = string.Empty;
    public ParentDto? Parent { get; set; }
}

// For profile tests
public class Product
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string InternalNotes { get; set; } = string.Empty;
}

public class ProductDto
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string InternalNotes { get; set; } = string.Empty;
}

// For record mapping tests
public class PersonSource
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Email { get; set; } = string.Empty;
    public AddressSource? HomeAddress { get; set; }
}

public class AddressSource
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

public record PersonRecord(Guid Id, string FirstName, string LastName, int Age);

// For constructor parameter resolution order tests
public class SourceWithFewProps
{
    public string Name { get; set; } = string.Empty;
    // intentionally omits Score and Label
}

/// <summary>Ctor has an optional parameter with an explicit default.</summary>
public record RecordWithOptionalParam(
    string Name,
    int Score = 42,
    string Label = "default-label");

/// <summary>Ctor has a required parameter with no default (must fall back to default(T)).</summary>
public record RecordWithRequiredParam(string Name, int RequiredScore, string? RequiredLabel);

public record PersonRecordWithAddress(Guid Id, string FirstName, int Age, AddressRecord? HomeAddress);

public record AddressRecord(string Street, string City);
