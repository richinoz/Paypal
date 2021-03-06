﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Mvc;
using Paypal.Models;

namespace Paypal.Controllers
{
    public class PaypalController : Controller
    {
        public PaypalController()
        {

        }
        //
        // GET: /Paypal/

        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Return()
        {
            return View();
        }

        public ActionResult Success(Order order)
        {
            return View(order);
        }

        [HttpPost]
        public ActionResult PostToPaypal(string item, string amountval)
        {
            var paypal = new Models.Paypal();
            //paypal.cmd = "_xclick";
            paypal.cmd = "_cart";
            paypal.business = ConfigurationManager.AppSettings["BusinessAccountKey"];

            var useSandbox = Convert.ToBoolean(ConfigurationManager.AppSettings["UseSandBox"]);
            ViewBag.actionUrl = useSandbox ? "https://www.sandbox.paypal.com/cgi-bin/webscr" : "https://www.paypal.com/cgi-bin/webscr";

            paypal.cancel_return = ConfigurationManager.AppSettings["CancelURL"];
            paypal.@return = ConfigurationManager.AppSettings["ReturnURL"];
            paypal.notify_url = ConfigurationManager.AppSettings["NotifyURL"];
            paypal.currency_code = ConfigurationManager.AppSettings["CurrencyCode"];

            paypal.PaypalItems.Add(new PaypalItem()
                                       {
                                           Amount = amountval,
                                           Name = item,
                                           Quantity = 1
                                       });

            paypal.PaypalItems.Add(new PaypalItem()
                                        {
                                            Amount = "12",
                                            Name = "test",
                                            Quantity = 1
                                        });

            return View(paypal);

        }

        public ActionResult IPN()
        {

            var formVals = new Dictionary<string, string>();
            formVals.Add("cmd", "_notify-validate");

            string response = GetPayPalResponse(formVals, true);

            if (response == "VERIFIED")
            {

                string transactionID = Request["txn_id"];
                string sAmountPaid = Request["mc_gross"];
                string orderID = Request["custom"];

                //_logger.Info("IPN Verified for order " + orderID);

                //validate the order
                Decimal amountPaid = 0;
                Decimal.TryParse(sAmountPaid, out amountPaid);

                //Order order = _orderService.GetOrder(new Guid(orderID));
                Order order = new Order() { ID = orderID };
                //check the Amount paid

                if (AmountPaidIsValid(order, amountPaid))
                {

                    var add = new Address
                                  {
                                      FirstName = Request["first_name"],
                                      LastName = Request["last_name"],
                                      Email = Request["payer_email"],
                                      Street1 = Request["address_street"],
                                      City = Request["address_city"],
                                      StateOrProvince = Request["address_state"],
                                      Country = Request["address_country"],
                                      Zip = Request["address_zip"],
                                      UserName = order.UserName
                                  };


                    //process itPAY
                    try
                    {
                        //_pipeline.AcceptPalPayment(order, transactionID, amountPaid);
                        //_logger.Info("IPN Order successfully transacted: " + orderID);
                        //return RedirectToAction("Success", "Paypal", new { order = order});
                        return View("Return");
                    }
                    catch
                    {
                        //HandleProcessingError(order, x);
                        return View("Return");
                    }
                }
                else
                {
                    //let fail - this is the IPN so there is no viewer
                }
            }



            return View("Return");
        }

        /// <summary>
        /// Handles the PDT Response from PayPal
        /// </summary>
        /// <returns></returns>
        //public ActionResult PDT()
        //{
        //    //_logger.Info("PDT Invoked");
        //    string transactionID = Request.QueryString["tx"];
        //    string sAmountPaid = Request.QueryString["amt"];
        //    string orderID = Request.QueryString["cm"];


        //    Dictionary<string, string> formVals = new Dictionary<string, string>();
        //    formVals.Add("cmd", "_notify-synch");
        //    formVals.Add("at", SiteData.PayPalPDTToken);
        //    formVals.Add("tx", transactionID);

        //    string response = GetPayPalResponse(formVals, true);
        //    //_logger.Info("PDT Response received: " + response);
        //    if (response.StartsWith("SUCCESS"))
        //    {
        //        //_logger.Info("PDT Response received for order " + orderID);

        //        //validate the order
        //        Decimal amountPaid = 0;
        //        Decimal.TryParse(sAmountPaid, out amountPaid);


        //        Order order = null;

        //        if (AmountPaidIsValid(order, amountPaid))
        //        {


        //            Address add = new Address();
        //            add.FirstName = GetPDTValue(response, "first_name");
        //            add.LastName = GetPDTValue(response, "last_name");
        //            add.Email = GetPDTValue(response, "payer_email");
        //            add.Street1 = GetPDTValue(response, "address_street");
        //            add.City = GetPDTValue(response, "address_city");
        //            add.StateOrProvince = GetPDTValue(response, "address_state");
        //            add.Country = GetPDTValue(response, "address_country");
        //            add.Zip = GetPDTValue(response, "address_zip");
        //            add.UserName = order.UserName;


        //            //process it
        //            try
        //            {
        //                // _pipeline.AcceptPalPayment(order, transactionID, amountPaid);
        //                // _logger.Info("PDT Order successfully transacted: " + orderID);
        //                return RedirectToAction("Receipt", "Order", new { id = order.ID });
        //            }
        //            catch
        //            {
        //                //HandleProcessingError(order, x);
        //                return View();
        //            }

        //        }
        //        else
        //        {
        //            //Payment Amount is off
        //            //this can happen if you have a Gift cert at PayPal
        //            //be careful of this!
        //            //HandleProcessingError(order, new InvalidOperationException("Amount paid (" + amountPaid.ToString("C") + ") was below the order total"));
        //            return View();
        //        }
        //    }
        //    else
        //    {
        //        ViewData["message"] = "Your payment was not successful with PayPal";
        //        return View();
        //    }
        //}
        /// <summary>
        /// Utility method for handling PayPal Responses
        /// </summary>
        string GetPayPalResponse(Dictionary<string, string> formVals, bool useSandbox)
        {

            string paypalUrl = useSandbox ? "https://www.sandbox.paypal.com/cgi-bin/webscr"
                : "https://www.paypal.com/cgi-bin/webscr";


            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(paypalUrl);

            // Set values for the request back
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";

            byte[] param = Request.BinaryRead(Request.ContentLength);
            string strRequest = Encoding.ASCII.GetString(param);

            var sb = new StringBuilder();
            sb.Append(strRequest);

            foreach (string key in formVals.Keys)
            {
                sb.AppendFormat("&{0}={1}", key, formVals[key]);
            }
            strRequest += sb.ToString();
            req.ContentLength = strRequest.Length;

            //for proxy
            //WebProxy proxy = new WebProxy(new Uri("http://urlort#");
            //req.Proxy = proxy;
            //Send the request to PayPal and get the response
            string response = "";
            using (StreamWriter streamOut = new StreamWriter(req.GetRequestStream(), System.Text.Encoding.ASCII))
            {

                streamOut.Write(strRequest);
                streamOut.Close();
                using (StreamReader streamIn = new StreamReader(req.GetResponse().GetResponseStream()))
                {
                    response = streamIn.ReadToEnd();
                }
            }

            return response;
        }
        bool AmountPaidIsValid(Order order, decimal amountPaid)
        {

            //pull the order
            bool result = true;

            if (order != null)
            {
                if (order.Total > amountPaid)
                {
                    //_logger.Warn("Invalid order Amount to PDT/IPN: " + order.ID + "; Actual: " + amountPaid.ToString("C") + "; Should be: " + order.Total.ToString("C") + "user IP is " + Request.UserHostAddress);
                    result = false;
                }
            }
            else
            {
                //_logger.Warn("Invalid order ID passed to PDT/IPN; user IP is " + Request.UserHostAddress);
            }
            return result;

        }
        string GetPDTValue(string pdt, string key)
        {

            string[] keys = pdt.Split('\n');
            string thisVal = "";
            string thisKey = "";
            foreach (string s in keys)
            {
                string[] bits = s.Split('=');
                if (bits.Length > 1)
                {
                    thisVal = bits[1];
                    thisKey = bits[0];
                    if (thisKey.Equals(key, StringComparison.InvariantCultureIgnoreCase))
                        break;
                }
            }
            return thisVal;


        }

    }
}
