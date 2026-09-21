using System.ComponentModel.DataAnnotations;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = "Library API";
        document.Info.Version = "v1";
        document.Info.Description = "API для керування каталогом книг.";
        return Task.CompletedTask;
    });
});

builder.Services.AddSingleton<BookRepository>();
builder.Services.AddAutoMapper(cfg => { }, typeof(Program).Assembly);

var app = builder.Build();

app.UseHttpsRedirection();
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.AddDocuments(["v1"]);
    options.WithTitle("Library API — документація");
});
app.MapControllers();
app.Run();

namespace LibraryApi.Models
{
    public enum BookGenre
    {
        Fiction,
        Classics,
        Dystopia,
        Poetry,
        Fantasy,
        ScienceFiction,
        Biography,
        History
    }

    public class Author
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public int BirthYear { get; set; }
    }

    public class Book
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public Author Author { get; set; } = new();
        public BookGenre Genre { get; set; }
        public int PublishedYear { get; set; }
        public decimal PurchasePrice { get; set; }
        public string InternalNotes { get; set; } = string.Empty;
    }
}

namespace LibraryApi.Dtos
{
    public class BookDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string AuthorFullName { get; set; } = string.Empty;
        public BookGenre Genre { get; set; }
        public int PublishedYear { get; set; }
        public int AuthorAgeAtPublication { get; set; }
    }

    public class CreateAuthorRequest
    {
        [Required(ErrorMessage = "Ім'я автора є обов'язковим.")]
        [MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Прізвище автора є обов'язковим.")]
        [MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Range(1000, 2100, ErrorMessage = "Рік народження має бути між 1000 та 2100 роками.")]
        public int BirthYear { get; set; }
    }

    public class CreateBookRequest
    {
        [Required(ErrorMessage = "Назва книги є обов'язковою.")]
        [MaxLength(100, ErrorMessage = "Назва книги не може перевищувати 100 символів.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Автор є обов'язковим.")]
        public CreateAuthorRequest Author { get; set; } = new();

        [Required(ErrorMessage = "Жанр є обов'язковим.")]
        public BookGenre Genre { get; set; }

        [Range(1000, 2100, ErrorMessage = "Рік видання має бути між 1000 та 2100 роками.")]
        public int PublishedYear { get; set; }
    }
}

namespace LibraryApi.Services
{
    using LibraryApi.Models;

    public class BookRepository
    {
        private readonly List<Book> _books = new();
        private int _nextId = 1;

        public BookRepository()
        {
            Add(new Book
            {
                Title = "1984",
                Author = new Author { FirstName = "Джордж", LastName = "Орвелл", BirthYear = 1903 },
                Genre = BookGenre.Dystopia,
                PublishedYear = 1949,
                PurchasePrice = 150.00m,
                InternalNotes = "Супер популярна книга, замовляти більше привезень."
            });

            Add(new Book
            {
                Title = "Великий Гетсбі",
                Author = new Author { FirstName = "Френсіс Скотт", LastName = "Фіцджеральд", BirthYear = 1896 },
                Genre = BookGenre.Classics,
                PublishedYear = 1925,
                PurchasePrice = 210.50m,
                InternalNotes = "Має пошкодження обкладинки на складі №2."
            });

            Add(new Book
            {
                Title = "Дюна",
                Author = new Author { FirstName = "Френк", LastName = "Герберт", BirthYear = 1920 },
                Genre = BookGenre.ScienceFiction,
                PublishedYear = 1965,
                PurchasePrice = 320.00m,
                InternalNotes = "Нова партія, перевірити якість друку."
            });
        }

        public List<Book> GetAll()
        {
            return _books.ToList();
        }

        public List<Book> GetByAuthor(string authorFragment)
        {
            return _books
                .Where(b =>
                    b.Author.FirstName.Contains(authorFragment, StringComparison.OrdinalIgnoreCase) ||
                    b.Author.LastName.Contains(authorFragment, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public Book? GetById(int id)
        {
            return _books.FirstOrDefault(b => b.Id == id);
        }

        public void Add(Book book)
        {
            book.Id = _nextId++;
            if (book.PurchasePrice == 0) book.PurchasePrice = 100.00m;
            _books.Add(book);
        }

        public bool Remove(int id)
        {
            var book = GetById(id);
            if (book is null) return false;
            _books.Remove(book);
            return true;
        }
    }
}

namespace LibraryApi.Mapping
{
    using AutoMapper;
    using LibraryApi.Dtos;
    using LibraryApi.Models;

    public class BookProfile : Profile
    {
        public BookProfile()
        {
            CreateMap<Book, BookDto>()
                .ForMember(dest => dest.AuthorFullName,
                    opt => opt.MapFrom(src => $"{src.Author.FirstName} {src.Author.LastName}".Trim()))
                .ForMember(dest => dest.AuthorAgeAtPublication,
                    opt => opt.MapFrom(src => src.PublishedYear - src.Author.BirthYear));

            CreateMap<CreateAuthorRequest, Author>();
            CreateMap<CreateBookRequest, Book>();
        }
    }
}

namespace LibraryApi.Controllers
{
    using AutoMapper;
    using LibraryApi.Dtos;
    using LibraryApi.Models;
    using LibraryApi.Services;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class BooksController : ControllerBase
    {
        private readonly BookRepository _repo;
        private readonly IMapper _mapper;

        public BooksController(BookRepository repo, IMapper mapper)
        {
            _repo = repo;
            _mapper = mapper;
        }

        [HttpGet]
        public ActionResult<List<BookDto>> GetAll([FromQuery] string? author = null)
        {
            var books = string.IsNullOrWhiteSpace(author)
                ? _repo.GetAll()
                : _repo.GetByAuthor(author.Trim());

            return Ok(_mapper.Map<List<BookDto>>(books));
        }

        [HttpGet("{id:int}")]
        public ActionResult<BookDto> GetById(int id)
        {
            var book = _repo.GetById(id);
            if (book is null) return NotFound();
            return Ok(_mapper.Map<BookDto>(book));
        }

        [HttpPost]
        [Consumes("application/json")]
        public ActionResult<BookDto> Create([FromBody] CreateBookRequest request)
        {
            var book = _mapper.Map<Book>(request);
            _repo.Add(book);
            var dto = _mapper.Map<BookDto>(book);
            return CreatedAtAction(nameof(GetById), new { id = book.Id }, dto);
        }

        [HttpPut("{id:int}")]
        [Consumes("application/json")]
        public IActionResult Update(int id, [FromBody] CreateBookRequest request)
        {
            var book = _repo.GetById(id);
            if (book is null) return NotFound();
            _mapper.Map(request, book);
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public IActionResult Delete(int id)
        {
            var removed = _repo.Remove(id);
            return removed ? NoContent() : NotFound();
        }
    }
}