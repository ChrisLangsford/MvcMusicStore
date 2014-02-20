using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace MvcMusicStore.Models
{
    public partial class ShoppingCart
    {
        MvcMusicStoreEntities storeDB = new MvcMusicStoreEntities();

        string ShoppingCartId { get; set; }

        public const string CART_SESSION_KEY = "CartId";

        public static ShoppingCart GetCart(HttpContextBase context)
        {
            var cart = new ShoppingCart();
            cart.ShoppingCartId = cart.GetCartId(context);
            return cart;
        }

        //helper method to simplify cart calls
        public static ShoppingCart GetCart(Controller controller)
        {
            return GetCart(controller.HttpContext);
        }

        public void AddToCart(Album album)
        { 
            //Get the matching cart and album instances
            var cartItem = storeDB.Carts.SingleOrDefault(
                c => c.CartId == ShoppingCartId
                && c.AlbumId == album.AlbumId);

            if (cartItem == null)
            {
                //Create a new cart item if no item exists
                cartItem = new Cart
                {
                    AlbumId = album.AlbumId,
                    CartId = ShoppingCartId,
                    Count = 1,
                    DateCreated = DateTime.Now
                };
                storeDB.Carts.Add(cartItem);
            }
            else
            { 
                //If the item does exist in the cart, then add one to the quantity
                cartItem.Count++;
            }

            //Save changes
            storeDB.SaveChanges();
        }

        public int RemoveFromCart(int id)
        { 
            //Get the cart
            var cartItem = storeDB.Carts.Single(
                cart => cart.CartId == ShoppingCartId
                && cart.RecordId == id);

            int itemCount = 0;

            if (cartItem != null)
            {
                if (cartItem.Count > 1)
                {
                    cartItem.Count--;
                    itemCount = cartItem.Count;
                }
                else
                {
                    storeDB.Carts.Remove(cartItem);
                }

                //Save Changes
                storeDB.SaveChanges();
            }
            return itemCount;
        }

        public void EmptyCart()
        {
            var cartItems = storeDB.Carts.Where(cart => cart.CartId == ShoppingCartId);

            foreach (var cartItem in cartItems)
            {
                storeDB.Carts.Remove(cartItem); 
            }

            //Save Changes
            storeDB.SaveChanges();
        }

        public List<Cart> GetCartItems()
        {
            return storeDB.Carts.Where(cart => cart.CartId == ShoppingCartId).ToList();
        }

        public int GetCount()
        { 
            //Get the count of each item in the cart and sum them up
            int? count = (from cartItems in storeDB.Carts
                          where cartItems.CartId == ShoppingCartId
                          select (int?)cartItems.Count).Sum();

            //Return 0 if all entries are null
            return count ?? 0;
        }

        public decimal GetTotal()
        { 
            //Multiply album price b count of that album to get the current price for each of those albums in the cart
            //summ all album price totals to get the cart total

            decimal? total = (from cartItems in storeDB.Carts
                              where cartItems.CartId == ShoppingCartId
                              select (int?)cartItems.Count * cartItems.Album.Price).Sum();
            return total ?? decimal.Zero;
        }

        public int CreateOrder(Order order)
        {
            decimal orderTotal = 0;

            var cartItems = GetCartItems();

            //Iterate over the items in the cart, adding the order details for each
            foreach (var item in cartItems)
            {
                var orderDetail = new OrderDetail
                {
                    AlbumId = item.AlbumId,
                    OrderId = order.OrderId,
                    UnitPrice = item.Album.Price,
                    Quantity = item.Count                
                };

                //Set the order total of the shopping cart
                orderTotal += (item.Count * item.Album.Price);

                storeDB.OrderDetails.Add(orderDetail);
            }

            //Set the order's total to the orderTotal count
            order.Total = orderTotal;

            //Save the changes
            storeDB.SaveChanges();

            //Empty the shopping cart
            EmptyCart();

            //Return the OrderId as the confirmation number
            return order.OrderId;
        }

        //We're useing HttpContextBase to allow access to cookies
        public string GetCartId(HttpContextBase context)
        {
            if (context.Session[CART_SESSION_KEY] == null)
            {
                if (!string.IsNullOrWhiteSpace(context.User.Identity.Name))
                {
                    context.Session[CART_SESSION_KEY] = context.User.Identity.Name;
                }
                else
                { 
                    //Generate a new random GUID using System.Guid class
                    Guid tempCartId = Guid.NewGuid();

                    //Send tempCartId to client as a cookie
                    context.Session[CART_SESSION_KEY] = tempCartId.ToString();
                }
            }
            return context.Session[CART_SESSION_KEY].ToString();
        }
        //When a user has logged in, migrate their shopping cart to be associated with their username
        public void MigrateCart(string userName)
        {
            var shoppingCart = storeDB.Carts.Where(c => c.CartId == ShoppingCartId);

            foreach (var item in shoppingCart)
            {
                item.CartId = userName;
            }
            storeDB.SaveChanges();
        }
    }
}