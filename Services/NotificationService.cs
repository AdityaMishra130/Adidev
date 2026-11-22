using CsharpProject.Data;
using CsharpProject.Models;
using Microsoft.EntityFrameworkCore;

namespace CsharpProject.Services;

public interface INotificationService
{
    Task SendNotificationAsync(string userId, string title, string message, string type = "Appointment", string? relatedUrl = null);
    Task<List<Notification>> GetUserNotificationsAsync(string userId, int limit = 10);
    Task<int> GetUnreadCountAsync(string userId);
    Task<bool> MarkAsReadAsync(int notificationId, string userId);
    Task MarkAllAsReadAsync(string userId);
}

public sealed class NotificationService(PortalDbContext database) : INotificationService
{
    public async Task SendNotificationAsync(string userId, string title, string message, string type = "Appointment", string? relatedUrl = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            RelatedUrl = relatedUrl,
            CreatedAtUtc = DateTime.UtcNow,
            IsRead = false
        };

        database.Notifications.Add(notification);
        await database.SaveChangesAsync();
    }

    public async Task<List<Notification>> GetUserNotificationsAsync(string userId, int limit = 10)
    {
        return await database.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return await database.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task<bool> MarkAsReadAsync(int notificationId, string userId)
    {
        var notification = await database.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
        if (notification is null) return false;

        notification.IsRead = true;
        await database.SaveChangesAsync();
        return true;
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        var unread = await database.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var item in unread)
        {
            item.IsRead = true;
        }

        await database.SaveChangesAsync();
    }
}
