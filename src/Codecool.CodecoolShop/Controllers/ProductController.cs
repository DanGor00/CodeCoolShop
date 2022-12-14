using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Codecool.CodecoolShop.Areas.Identity.Data;
using Codecool.CodecoolShop.Areas.Identity.Extension;
using Codecool.CodecoolShop.Models;
using Data;
using Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Address = Domain.Address;
using Order = Domain.Order;
using PaymentInfo = Domain.PaymentInfo;
using Product = Domain.Product;

namespace Codecool.CodecoolShop.Controllers;

public class ModelContainer
{
    public List<Product> products { get; set; }
    public List<OrderedProduct> OrderedProducts { get; set; }
}

public class ProductController : Controller
{
    private readonly CodecoolShopContext _context;
    private readonly ILogger<ProductController> _logger;
    private readonly UserManager<CodecoolCodecoolShopUser> _userManager;

    public ProductController(ILogger<ProductController> logger, CodecoolShopContext context,
        UserManager<CodecoolCodecoolShopUser> userManager)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
    }

    public IActionResult Index()
    {
        List<OrderedProduct> orderedProducts = null;

        var userId = _userManager.GetUserId(User);
        var fullname = User.Identity.GetFullName();
        orderedProducts = _context.OrderedProducts
            .Include(p => p.Order)
            .Where(p => p.Order.User_id == userId && p.Order.OrderPayed == "No")
            .ToList();

        var products = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .ToList();

        var model = new ModelContainer {OrderedProducts = orderedProducts, products = products};
        _logger.LogInformation("Product page loaded...");
        return View(model);
    }

    public IActionResult SortByCategory(int id)
    {
        List<OrderedProduct> orderedProducts = null;

        var userId = _userManager.GetUserId(User);
        orderedProducts = _context.OrderedProducts
            .Include(p => p.Order)
            .Where(p => p.Order.User_id == userId && p.Order.OrderPayed == "No")
            .ToList();

        var products = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .Where(p => p.Category.Id == id)
            .ToList();
        var model = new ModelContainer {OrderedProducts = orderedProducts, products = products};
        _logger.LogInformation("Products sorted by category...");
        return View("Index", model);
    }

    public IActionResult SortBySupplier(int id)
    {
        List<OrderedProduct> orderedProducts = null;

        var userId = _userManager.GetUserId(User);
        orderedProducts = _context.OrderedProducts
            .Include(p => p.Order)
            .Where(p => p.Order.User_id == userId && p.Order.OrderPayed == "No")
            .ToList();

        var products = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .Where(p => p.Supplier.Id == id)
            .ToList();
        var model = new ModelContainer {OrderedProducts = orderedProducts, products = products};
        _logger.LogInformation("Products sorted by supplier...");
        return View("Index", model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel {RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier});
    }

    public IActionResult Add(int? id)
    {
        _logger.LogInformation("Attempt Product adding to cart");
        if (User.Identity.IsAuthenticated)
        {
            if (ProductAlreadyInCart(id))
            {
                _logger.LogInformation("Product detected in cart; Adding +1 quantity.");
                IncreaseProductQuantity(id);
            }
            else
            {
                AddNewProductToCart(id);
            }
        }

        return RedirectToAction("Index");
    }

    private void IncreaseProductQuantity(int? id)
    {
        var product = _context.OrderedProducts.Include(p => p.Order)
            .First(p => p.ProductId == id && p.Order.OrderPayed == "No");
        product.Quantity++;
        _context.SaveChanges();
    }

    private void AddNewProductToCart(int? id)
    {
        var userId = _userManager.GetUserId(User);
        var product = _context.Products.Where(p => p.Id == id).First();
        Order order = null;
        if (!UserHasOrder(userId))
        {
            var address = new Address
            {
                City = "",
                Country = "",
                Email = "",
                FullName = "",
                Phone = "",
                Street = "",
                Zip = ""
            };
            var payment = new PaymentInfo
            {
                NameOnCard = "",
                CardNumber = "",
                CVV = "",
                ExpMonth = "",
                ExpYear = ""
            };
            order = new Order {Address = address, User_id = userId, PaymentInfo = payment, OrderPayed = "No"};
            _context.Addresses.Add(address);
            _context.Orders.Add(order);
        }

        if (UserHasOrder(userId)) order = _context.Orders.First(o => o.User_id == userId && o.OrderPayed == "No");

        var orderedProduct = new OrderedProduct
        {
            Currency = product.Currency,
            Name = product.Name,
            Price = product.DefaultPrice,
            Order = order,
            ProductId = product.Id,
            Quantity = 1
        };
        _context.OrderedProducts.Add(orderedProduct);
        _context.SaveChanges();
    }

    private bool UserHasOrder(string userId)
    {
        return _context.Orders.Any(o => o.User_id == userId && o.OrderPayed == "No");
    }

    private bool ProductAlreadyInCart(int? id)
    {
        var userId = _userManager.GetUserId(User);
        return _context.OrderedProducts
            .Include(p => p.Order)
            .Any(p => p.ProductId == id && p.Order.User_id == userId && p.Order.OrderPayed == "No");
    }

    public IActionResult Minus(int? id)
    {
        if (User.Identity.IsAuthenticated)
            if (ProductAlreadyInCart(id))
                DecreaseProductQuantity(id);

        return RedirectToAction("Index");
    }

    private void DecreaseProductQuantity(int? id)
    {
        var product = _context.OrderedProducts.Include(p => p.Order)
            .First(p => p.ProductId == id && p.Order.OrderPayed == "No");
        product.Quantity--;
        if (product.Quantity == 0)
            _context.OrderedProducts.Remove(product);
        _context.SaveChanges();
    }

    public IActionResult Delete(int? id)
    {
        if (User.Identity.IsAuthenticated)
            if (ProductAlreadyInCart(id))
            {
                var userId = _userManager.GetUserId(User);
                var product = _context.OrderedProducts
                    .Include(p => p.Order)
                    .First(p => p.ProductId == id && p.Order.User_id == userId && p.Order.OrderPayed == "No");
                _context.OrderedProducts.Remove(product);
                _context.SaveChanges();
            }

        return RedirectToAction("Index");
    }
}