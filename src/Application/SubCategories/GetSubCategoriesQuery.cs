using Application.Abstractions.Messaging;
using Application.SubCategories.Data;

namespace Application.SubCategories;

/// <param name="CategoryId">Optional parent-category filter.</param>
public sealed record GetSubCategoriesQuery(Guid? CategoryId) : IQuery<List<SubCategoryResponse>>;
