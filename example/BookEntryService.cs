using System;
using System.Collections.Generic;

namespace Example
{
    // Interface para o serviço
    public interface IBookEntryService
    {
        void AddBook(BookEntry book);
        List<BookEntry> GetAllBooks();
        BookEntry GetBookById(int id);
        void DeleteBook(int id); // Este método NÃO é usado - deveria ser detectado como código morto
    }
    
    // Implementação do serviço - usado via injeção de dependência
    public class BookEntryService : IBookEntryService
    {
        private readonly List<BookEntry> _books = new();
        
        public void AddBook(BookEntry book)
        {
            book.Id = _books.Count + 1;
            _books.Add(book);
        }
        
        public List<BookEntry> GetAllBooks()
        {
            return _books;
        }
        
        public BookEntry GetBookById(int id)
        {
            return _books.Find(b => b.Id == id);
        }
        
        // Este método NÃO é usado em lugar nenhum - deveria ser detectado como código morto
        public void DeleteBook(int id)
        {
            var book = _books.Find(b => b.Id == id);
            if (book != null)
            {
                _books.Remove(book);
            }
        }
    }
    
    public class BookEntry
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string Category { get; set; }
    }
    
    // Controller que usa o serviço via injeção de dependência
    public class BookController
    {
        private readonly IBookEntryService _bookService;
        
        // Injeção de dependência via construtor
        public BookController(IBookEntryService bookService)
        {
            _bookService = bookService;
        }
        
        public void CreateBook(string title, string author)
        {
            var book = new BookEntry { Title = title, Author = author };
            _bookService.AddBook(book); // Usa o serviço injetado
        }
        
        public void ListBooks()
        {
            var books = _bookService.GetAllBooks(); // Usa o serviço injetado
            foreach (var book in books)
            {
                Console.WriteLine($"{book.Id}: {book.Title} by {book.Author}");
            }
        }
        
        public void ShowBook(int id)
        {
            var book = _bookService.GetBookById(id); // Usa o serviço injetado
            if (book != null)
            {
                Console.WriteLine($"Book: {book.Title} by {book.Author}");
            }
        }
        
        // Note: DeleteBook do serviço NÃO é usado em lugar nenhum
    }
    
    // Configuração de DI (simulada)
    public class Startup
    {
        public void ConfigureServices()
        {
            // Registro do serviço no container de DI
            // services.AddScoped<IBookEntryService, BookEntryService>();
        }
    }
} 