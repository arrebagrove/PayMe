﻿using PayMe.Core.Events;
using PayMe2.ViewModels.Abscenses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using PayMe2.Infrastructure;
using PayMe.Core;
using PayMe.Core.Eventprocessing;
using PayMe.Core.Entities;

namespace PayMe2.Controllers
{
    public class AbscenseController : Controller
    {
        // GET: Abscense
        public ActionResult Index(Guid instanceId)
        {
            using (var db = Context.Create())
            {
                var abscenses = db.Abscenses.AsNoTracking().Where(a => a.InstanceId == instanceId).ToList();
                var byPersons = abscenses.GroupBy(g => g.UserId).Select(g => new AbscensesByPerson
                {
                    UserId = g.Key,
                    Abscenses = g.OrderBy(x => x.From).ToList(),
                    Sum = g.Sum(x => (int)(x.To - x.From).TotalDays + 1)
                }).ToList();

                var persons = db.UserToInstanceMappings.Where(x => x.InstanceId == instanceId).Select(x => x.User).ToDictionary(x => x.Id);
                foreach (var byPerson in byPersons)
                {
                    byPerson.User = persons[byPerson.UserId];
                }

                return View(new IndexViewModel
                {
                    InstanceId = instanceId,
                    Persons = byPersons
                });
            }
        }

        public ActionResult Create(Guid instanceId)
        {
            return View(new CreateViewModel
            {
                InstanceId = instanceId,
                FromDate = DateTime.UtcNow.Date,
                ToDate = DateTime.UtcNow.Date,
            });
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public ActionResult Create(Guid instanceId, CreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                var ev = AbscenseEventFactory.CreateAbscense(instanceId, Guid.NewGuid(), model.Description, model.FromDate, model.ToDate, this.GetAudit());
                using (var db = Context.Create())
                {
                    db.StoredEvents.Add(StoredEvent.FromEvent(ev));
                    EventProcessor.Process(db, ev);
                    db.SaveChanges();
                    return RedirectToAction("Index", new { instanceId });
                }
            }
            return View(model);
        }

        public ActionResult Edit(Guid instanceId, Guid abscenseId)
        {

            using (var db = Context.Create())
            {
                var userId = this.GetAudit().UserId;
                var abscense = db.Abscenses.First(a => a.InstanceId == instanceId && a.Id == abscenseId && a.UserId == userId);
                return View(new CreateViewModel
                {
                    InstanceId = instanceId,
                    FromDate = abscense.From,
                    ToDate = abscense.To,
                });
            }
           
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public ActionResult Edit(Guid instanceId, Guid abscenseId, CreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                using (var db = Context.Create())
                {
                    var ev = AbscenseEventFactory.EditAbscense(instanceId, abscenseId, model.Description, model.FromDate, model.ToDate, this.GetAudit());                       
                    db.StoredEvents.Add(StoredEvent.FromEvent(ev));
                    EventProcessor.Process(db, ev);
                    db.SaveChanges();
                    return RedirectToAction("Index", new { instanceId });
                }
            }
            return View(model);
        }
    }
}