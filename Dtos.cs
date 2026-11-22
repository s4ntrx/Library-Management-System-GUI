namespace LibraryManagement.Web;

public sealed record BookDto(
    int Id, string Title, string Author, string Isbn, string Status,
    int? BorrowedByMemberId, string? BorrowedByName, DateOnly? DueOn, int DaysOverdue);

public sealed record MemberDto(int Id, string Name, string Email, int ActiveLoans);
public sealed record ReturnDto(BookDto Book, int DaysOverdue);

public sealed record NewBookRequest(string? Title, string? Author, string? Isbn);
public sealed record NewMemberRequest(string? Name, string? Email);
public sealed record BorrowRequest(int MemberId);

public sealed record ErrorBody(string Error);
