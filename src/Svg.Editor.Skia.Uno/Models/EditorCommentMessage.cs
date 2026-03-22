using System;

namespace Svg.Editor.Skia.Uno.Models;

public sealed class EditorCommentMessage
{
    public EditorCommentMessage(int id, string authorName, string text, DateTimeOffset createdAt, bool isMine)
    {
        Id = id;
        AuthorName = authorName;
        AuthorInitials = BuildInitials(authorName);
        Text = text;
        CreatedAt = createdAt;
        IsMine = isMine;
        TimestampText = FormatTimestamp(createdAt);
    }

    public int Id { get; }

    public string AuthorName { get; }

    public string AuthorInitials { get; }

    public string Text { get; }

    public DateTimeOffset CreatedAt { get; }

    public bool IsMine { get; }

    public string TimestampText { get; }

    private static string BuildInitials(string authorName)
    {
        if (string.IsNullOrWhiteSpace(authorName))
        {
            return "?";
        }

        var parts = authorName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][0].ToString().ToUpperInvariant(),
            _ => string.Concat(parts[0][0], parts[^1][0]).ToUpperInvariant()
        };
    }

    private static string FormatTimestamp(DateTimeOffset createdAt)
    {
        var elapsed = DateTimeOffset.Now - createdAt;
        if (elapsed.TotalMinutes < 2)
        {
            return "Just now";
        }

        if (elapsed.TotalMinutes < 60)
        {
            return $"{Math.Max(1, (int)Math.Round(elapsed.TotalMinutes))}m";
        }

        if (elapsed.TotalHours < 24)
        {
            return $"{Math.Max(1, (int)Math.Round(elapsed.TotalHours))}h";
        }

        if (elapsed.TotalDays < 7)
        {
            return $"{Math.Max(1, (int)Math.Round(elapsed.TotalDays))}d";
        }

        return createdAt.ToLocalTime().ToString("dd MMM");
    }
}
