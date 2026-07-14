using Application.Abstractions.Messaging;
using Application.SubCategories.Data;

namespace Application.SubCategories;

/// <param name="Category">Optional parent-category filter.</param>
public sealed record GetSubCategoriesQuery(string? Category) : IQuery<List<SubCategoryResponse>>;
