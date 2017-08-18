using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
 using System;
using csharpbelt.Models;
using System.Collections.Generic;
using System.Linq;
 using System.Threading.Tasks;
 using Microsoft.EntityFrameworkCore;
namespace csharpbelt.Controllers
{
    public class AuctionController : Controller
    {
          private auctionContext _context;
         
        public AuctionController(auctionContext context) {
            _context = context;
        }

        
[HttpGet]
 
[Route("Home")]
        public IActionResult Home()
        {
            if(HttpContext.Session.GetInt32("UserId") == null){
                return RedirectToAction("Index", "User");
            }
            
        ViewBag.CurrUser = _context.user.SingleOrDefault(u => u.UserId == HttpContext.Session.GetInt32("UserId"));
            // ViewBag.Auctions = _context.auction.ToList();

        //     List<AuctionObj> Auctions = _context.auction.OrderBy(b => b.EndDate)
        //           .Include(p=>p.Bids)  
        //           .Include(p=>p.CreatedBy)  
        //           .ToList();
            
        //     ViewBag.Auctions = Auctions;
           
        //     ViewBag.Bids = _context.bid.ToList();
        // return View("Home");

         List<Auction> auctions = _context.auctions.Include(a => a.User).OrderBy(a => a.EndDate).ToList();
            foreach (var auction in auctions) {
                if ((auction.EndDate - DateTime.Now).TotalDays <= 0) {
                    List<Bid> bids = _context.bids.Where(b => b.AuctionId == auction.AuctionId).ToList();
                    foreach (var bid in bids) {
                        //found highest bidder
                        if (bid.Amount == auction.Bid) {
                            //give money to auction creator
                            Wallet auction_wallet = _context.wallets.SingleOrDefault(w => w.UserId == auction.UserId);
                            auction_wallet.Amount += bid.Amount;
                        } else {
                            //give money back to bidder
                            Wallet bidder_wallet = _context.wallets.SingleOrDefault(w => w.UserId == bid.UserId);
                            bidder_wallet.Amount += bid.Amount;
                        }
                        _context.bids.Remove(bid);
                    }
                    _context.auctions.Remove(auction);
                    _context.SaveChanges();
                }
            }
            ViewBag.Auctions = _context.auctions.Include(a => a.User).OrderBy(a => a.EndDate).ToList();
            ViewBag.UserId = HttpContext.Session.GetInt32("userid");
            ViewBag.Wallet = _context.wallets.Include(w => w.User).SingleOrDefault(w => w.UserId == HttpContext.Session.GetInt32("userid"));       
            return View();
        }

[HttpGet]
        [Route("Create")]
        public IActionResult Create(){
            if(HttpContext.Session.GetInt32("UserId") == null){
                return RedirectToAction("Index", "User");
            }
            return View("Create");
        }
[HttpPost]
        [Route("Add")]
        public IActionResult Add(AuctionObjViewModel test){
            if(!ModelState.IsValid){
                return View("Create");
            }
            // check for positive starting bid
            if(test.StartingBid <= 0){
                ViewBag.BidError = "Bid Amount Must Be > 0";
                return View("Create");
            }
            // check for future end date
            if(test.EndDate < DateTime.Now){
                System.Console.WriteLine("*********** date in the passssstttttts ***********************");
                ViewBag.DateError = "Date Must Be In The Future";
                return View("Create");
            }

            // add Auction to DB
            AuctionObj newAuct = new AuctionObj{
                Name = test.Name,
                Description = test.Description,
                StartingBid = test.StartingBid,
                EndDate = test.EndDate,
                UserId = (int)HttpContext.Session.GetInt32("UserId")
            };
            _context.auction.Add(newAuct);
            _context.SaveChanges();

            // add starting bid to auction
            Bid FirstBid = new Bid{
                Amount = test.StartingBid,
                CreatedAt = DateTime.Now,
                UserId = (int)HttpContext.Session.GetInt32("UserId"),
                AuctionObjId = newAuct.AuctionObjId 
            };
            _context.bid.Add(FirstBid);
            _context.SaveChanges();


            return RedirectToAction("Home");
            
        }

         [HttpGet]
        [Route("Auction/{id}")]
        public IActionResult Auction(int id){
            if(HttpContext.Session.GetInt32("UserId") == null){
                return RedirectToAction("Index", "User");
            }
            
            
            AuctionObj auct =  _context.auction.SingleOrDefault(a => a.AuctionObjId == id);
            ViewBag.Auction = auct;
            
            ViewBag.Creator = _context.user.SingleOrDefault(u => u.UserId == auct.UserId);
            //ViewBag.Bid = _context.bid.Where(b => b.AuctionObjId == id).OrderByDescending(b => b.CreatedAt).First();
            
          ViewBag.Bid  = _context.bid.OrderByDescending(b => b.Amount)
            .Include(p=>p.Bidder)
            .FirstOrDefault(b => b.AuctionObjId == id);
            return View();
        }


[HttpPost]
        [Route("Bid/{id}")]
        public IActionResult Bid(int id, int bid){
            // bid must be greater than most recent bid && less than wallet
          Userrecord CurrUser = _context.user.SingleOrDefault(u => u.UserId == HttpContext.Session.GetInt32("UserId"));
            Bid lastBid = _context.bid.Where(b => b.AuctionObjId == id).OrderByDescending(b => b.CreatedAt).FirstOrDefault();
            
            AuctionObj auct =  _context.auction.SingleOrDefault(a => a.AuctionObjId == id);
            ViewBag.Auction = auct;
            ViewBag.Creator = _context.user.SingleOrDefault(u => u.UserId == auct.UserId);
            ViewBag.Bid = _context.bid.Where(b => b.AuctionObjId == id).OrderByDescending(b => b.CreatedAt).FirstOrDefault();
            
            if(bid < lastBid.Amount || bid > CurrUser.Wallet){
                ViewBag.BidError = "That Bid is Not Valid ";
                return View("Auction", new {id=id});
            }
            // if it is valid add the bid
            Bid FirstBid = new Bid{
                Amount = bid,
                CreatedAt = DateTime.Now,
                UserId = (int)HttpContext.Session.GetInt32("UserId"),
                AuctionObjId = id 
            };
            _context.bid.Add(FirstBid);
            _context.SaveChanges();

            // decrease users wallet

            CurrUser.Wallet -= bid;
            _context.SaveChanges();
            return RedirectToAction("Auction", new {id=id});
        }

        

        [HttpGet]
        [Route("Delete/{id}")]
        public IActionResult Delete(int id){
              if (HttpContext.Session.GetInt32("UserId") == null) {
                return RedirectToAction("Index", "User");
            }
            AuctionObj delauction = _context.auction.SingleOrDefault(a => a.AuctionObjId == id);
            List<Bid> delbids = _context.bid.Where(b => b.AuctionObjId == id).ToList();
            if (delauction.UserId == HttpContext.Session.GetInt32("UserId")) {
                _context.auction.Remove(delauction);
                foreach (var bid in delbids) {
                    _context.bid.Remove(bid);
                }
                _context.SaveChanges();
            }
            return RedirectToAction("Home");
            
        
























}
    }
}
