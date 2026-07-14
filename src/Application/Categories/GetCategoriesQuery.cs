using Application.Abstractions.Messaging;
using Application.Categories.Data;

namespace Application.Categories;

public sealed record GetCategoriesQuery : IQuery<List<CategoryResponse>>;
