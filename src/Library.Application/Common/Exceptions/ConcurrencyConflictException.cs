namespace Library.Application.Common.Exceptions;

/// <summary>
/// נזרקת מ-<see cref="Interfaces.IUnitOfWork.SaveChangesAsync"/> כששמירה נכשלה כי בקשה אחרת שינתה
/// את אותה שורה בינתיים (ה-Version בבסיס הנתונים כבר לא זה שנקרא).
/// היא מחליפה את חריגת ההתנגשות של EF Core, כדי ששכבת Application לא תכיר את EF:
/// התרגום נעשה ב-UnitOfWork שב-Infrastructure, והחריגה המקורית נשמרת ב-InnerException.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
