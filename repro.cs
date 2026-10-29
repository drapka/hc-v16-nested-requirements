#:package HotChocolate.Data@16.8.0-p.3

using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Types;
using Microsoft.Extensions.DependencyInjection;

var executor = await new ServiceCollection()
    .AddGraphQL()
    .AddQueryType<Query>()
    .AddTypeExtension(typeof(BookExtensions))
    .AddProjections()
    .BuildRequestExecutorAsync();

// Expected for every query: name "Jane Doe", and authorProfileUrl "/authors/42" wherever it is selected.
// Observed per HC version (✅ expected result, ❌ wrong result or error):
string[] queries =
[
    // Bug: client selects author { name } AND the field whose requirement is author { id }.
    // 16.6.9 ❌ HC0018 (author.name null) | 16.8.0-p.3 ❌ HC0018 (author.name null)
    "{ books { author { name } authorProfileUrl } }",
    // 16.6.9 ❌ "/authors/0" (requirement dropped on paged roots) | 16.8.0-p.3 ❌ HC0018 (author.name null)
    "{ booksCursor { nodes { author { name } authorProfileUrl } } }",

    // Controls.
    // No requirement in play. 16.6.9 ✅ | 16.8.0-p.3 ✅
    "{ booksCursor { nodes { author { name } } } }",
    // Requirement alone. 16.6.9 ❌ NullReferenceException (requirement dropped, author not projected) | 16.8.0-p.3 ✅
    "{ booksCursor { nodes { authorProfileUrl } } }",
    // Client also selects the required leaf. 16.6.9 ✅ | 16.8.0-p.3 ❌ HC0018 (requirement still binds author a second time)
    "{ booksCursor { nodes { author { name id } authorProfileUrl } } }",
];

foreach (var query in queries)
{
    var result = await executor.ExecuteAsync(query);
    Console.WriteLine(query);
    Console.WriteLine(result.ToJson());
    Console.WriteLine();
}

public class Query
{
    private static readonly Book[] Books =
    [
        new() { Id = 1, Title = "Some Book", Author = new Author { Id = 42, Name = "Jane Doe" } },
    ];

    [UseProjection]
    public IQueryable<Book> GetBooks() => Books.AsQueryable();

    [UsePaging]
    [UseProjection]
    public IQueryable<Book> GetBooksCursor() => Books.AsQueryable();
}

public class Book
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required Author Author { get; set; }
}

public class Author
{
    public int Id { get; set; }
    public required string Name { get; set; }
}

[ExtendObjectType<Book>]
public static class BookExtensions
{
    public static string GetAuthorProfileUrl([Parent(requires: "{ Author { Id } }")] Book book)
        => $"/authors/{book.Author.Id}";
}
