using LibraryManagement.Models;
using LibraryManagement.Services;

namespace LibraryManagement.Web;

/// <summary>
/// LibraryService is not thread-safe, and a web server handles requests in parallel.
/// Every call goes through one lock and returns plain copies, never the live objects.
/// </summary>
public sealed class LibraryGateway(LibraryService library, TimeProvider clock)
{
    private readonly object gate = new();

    public IReadOnlyList<BookDto> ListBooks(string? query)
    {
        lock (gate)
        {
            var books = string.IsNullOrWhiteSpace(query) ? library.Books : library.Search(query);
            return books.Select(BookToDto).ToList();
        }
    }

    public BookDto? FindBook(int id)
    {
        lock (gate)
        {
            var book = library.Books.FirstOrDefault(b => b.Id == id);
            return book is null ? null : BookToDto(book);
        }
    }

    public BookDto AddBook(NewBookRequest request)
    {
        lock (gate)
            return BookToDto(library.AddBook(request.Title ?? "", request.Author ?? "", request.Isbn ?? ""));
    }

    public IReadOnlyList<MemberDto> ListMembers()
    {
        lock (gate)
            return library.Members.Select(MemberToDto).ToList();
    }

    public MemberDto AddMember(NewMemberRequest request)
    {
        lock (gate)
            return MemberToDto(library.RegisterMember(request.Name ?? "", request.Email ?? ""));
    }

    public BookDto Borrow(int bookId, int memberId)
    {
        lock (gate)
            return BookToDto(library.Borrow(bookId, memberId));
    }

    public ReturnDto Return(int bookId)
    {
        lock (gate)
        {
            var receipt = library.Return(bookId);
            return new ReturnDto(BookToDto(receipt.Book), receipt.DaysOverdue);
        }
    }

    private BookDto BookToDto(Book book)
    {
        var borrower = book.BorrowedByMemberId is { } memberId
            ? library.Members.FirstOrDefault(m => m.Id == memberId)?.Name
            : null;
        return new BookDto(book.Id, book.Title, book.Author, book.Isbn, book.Status.ToString(),
            book.BorrowedByMemberId, borrower, book.DueOn, DaysOverdue(book));
    }

    private MemberDto MemberToDto(Member member) =>
        new(member.Id, member.Name, member.Email, library.Books.Count(b => b.BorrowedByMemberId == member.Id));

    private int DaysOverdue(Book book)
    {
        if (book.Status != BookStatus.Borrowed || book.DueOn is not { } due)
            return 0;
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        return Math.Max(0, today.DayNumber - due.DayNumber);
    }
}
