namespace Flicked.Api.Models; 
public record NewsPost(string Id, string Category, DateOnly Date, string? Badge, string Title, string Excerpt, string[] Body);
