using JiraFit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JiraFit.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        
        builder.Property(u => u.PhoneNumber)
            .IsRequired()
            .HasMaxLength(50);
            
        builder.HasIndex(u => u.PhoneNumber).IsUnique();

        builder.Property(u => u.Name).IsRequired(false).HasMaxLength(150);
        
        builder.Property(u => u.Gender).HasConversion<string>();
        builder.Property(u => u.Objective).HasConversion<string>();

        builder.HasMany(u => u.Meals)
            .WithOne(m => m.User)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class MealConfiguration : IEntityTypeConfiguration<Meal>
{
    public void Configure(EntityTypeBuilder<Meal> builder)
    {
        builder.HasKey(m => m.Id);
    }
}
