using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Categories;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users;

internal sealed class RegisterUserCommandHandler(
    IApplicationDbContext dbContext,
    IPasswordHasher passwordHasher)
    : ICommandHandler<RegisterUserCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        RegisterUserCommand command,
        CancellationToken cancellationToken)
    {
        if ((command.Password ?? string.Empty).Length < 8)
        {
            return Result.Failure<Guid>(UserErrors.PasswordTooShort);
        }

        string email = (command.Email ?? string.Empty).Trim().ToLowerInvariant();
        string username = (command.Username ?? string.Empty).Trim().ToLowerInvariant();

        if (await dbContext.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.EmailNotUnique);
        }

        if (await dbContext.Users.AnyAsync(u => u.Username == username, cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.UsernameNotUnique);
        }

        string passwordHash = passwordHasher.Hash(command.Password!);

        Result<User> userResult = User.Create(command.Email!, command.Username!, command.DisplayName!, passwordHash);
        if (userResult.IsFailure)
        {
            return Result.Failure<Guid>(userResult.Error);
        }

        dbContext.Users.Add(userResult.Value);

        foreach ((string code, string name, string icon) in SystemCategories.All)
        {
            Result<Category> category = Category.Create(userResult.Value.Id, name, icon, code);
            if (category.IsFailure)
            {
                return Result.Failure<Guid>(category.Error);
            }

            dbContext.Categories.Add(category.Value);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return userResult.Value.Id;
    }
}
