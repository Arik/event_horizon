using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Economy;
using Economy.Products;
using Galaxy.StarContent;
using Game;
using Game.Exploration;
using GameDatabase.DataModel;
using GameModel;
using GameModel.Quests;
using GameServices;
using GameServices.Quests;
using GameServices.Random;
using Model.Generators;
using Session;
using Session.Content;
using UnityEngine;
using Utils;
using Zenject;
using Random = System.Random;

namespace Galaxy
{
    public sealed class StarData : GameServiceBase
    {
        [Inject] private readonly RegionMap _regionMap;
        [Inject] private readonly IRandom _random;
        [Inject] private readonly HolidayManager _holidayManager;
        [Inject] private readonly IQuestManager _questManager;
        [Inject] private StarContentChangedSignal.Trigger _starContentChangedTrigger;

        [Inject] private readonly Occupants _occupants;
        [Inject] private readonly Boss _boss;
        [Inject] private readonly Ruins _ruins;
        [Inject] private readonly Challenge _challenge;
        [Inject] private readonly LocalEvent _localEvent;
        [Inject] private readonly Survival _survival;
        [Inject] private readonly Wormhole _wormhole;
        [Inject] private readonly StarBase _starBase;
        [Inject] private readonly XmasTree _xmas;
        [Inject] private readonly Hive _hive;

        [Inject] private readonly InventoryFactory _inventoryFactory;
        [Inject] private readonly Planet.Factory _planetFactory;

        [Inject]
        public StarData(ISessionData session, SessionDataLoadedSignal dataLoadedSignal, SessionCreatedSignal sessionCreatedSignal)
            : base(dataLoadedSignal, sessionCreatedSignal)
        {
            _session = session;
        }

        public bool IsVisited(int starId) { return _session.StarMap.IsVisited(starId); }
        public void SetVisited(int starId) { _session.StarMap.SetVisited(starId); }
        public Vector2 GetPosition(int starId) { return StarLayout.GetStarPosition(starId, _random.Seed); }
        public int GetLevel(int starId) { return StarLayout.GetStarLevel(starId, _random.Seed); }
        public string GetName(int starId) { return NameGenerator.GetStarName(starId); }
        public string GetBookmark(int starId) { return _session.StarMap.GetBookmark(starId); }

        public void SetBookmark(int starId, string value)
        {
            _session.StarMap.SetBookmark(starId, value);
            _starContentChangedTrigger.Fire(starId);
        }

        public void SetFiltered(int starId)
        {
            _filteredstars[starId] = ShouldFilter(starId) && _filter != "";
            // _starContentChangedTrigger.Fire(starId);
        }

        private string _filter = "";
        private Dictionary<int, bool> _filteredstars = new Dictionary<int, bool>();

        public string Filter
        {
            get { return _filter; }
            set
            {
                if (value == _filter)
                    return;

                _filter = value;
                _filteredstars.Clear();
            }
        }

        public bool ShouldFilter(int starId)
        {
            if (GetBookmark(starId) == _filter) return true;

            var objects = GetObjects(starId);

            if (new Regex("(\\s|^)terran(\\s|$)").IsMatch(_filter))
            {
                foreach (Planet planet in _planetFactory.CreatePlanets(starId))
                {
                    if (planet.Type == PlanetType.Terran) return true;
                }
            }

            if (new Regex("(\\s|^)event(\\s|$)").IsMatch(_filter))
            {
                if (objects.Contain(StarObjectType.Event) && GetLocalEvent(starId).IsActive) return true;
            }

            if (objects.Contain(StarObjectType.BlackMarket))
            {
                foreach (IProduct product in _inventoryFactory.CreateBlackMarketInventory(starId).Items)
                {
                    if (new Regex("(\\s|^)("+ product.Type.Name.ToLower() + "|"+ product.Type.Id.ToLower() + ")(\\s|$)").IsMatch(_filter)) return true;
                }
            }

            if (HasStarBase(starId))
            {
                foreach (IProduct product in _inventoryFactory.CreateFactionInventory(GetRegion(starId)).Items)
                {
                    if (new Regex("(\\s|^)(" + product.Type.Name.ToLower() + "|" + product.Type.Id.ToLower() + ")(\\s|$)").IsMatch(_filter)) return true;
                }
            }

            return false;
        }

        public bool HasBookmark(int starId) { return _session.StarMap.HasBookmark(starId); }

        public bool IsFiltered(int starId)
        {
            if (!_filteredstars.ContainsKey(starId))
                SetFiltered(starId);
            return _filteredstars[starId];
        }

        public Region GetRegion(int starId) { return _regionMap.GetStarRegion(starId); }

        public bool IsQuestObjective(int starId) { return _questManager.IsQuestObjective(starId); }

        public StarObjects GetObjects(int starId)
        {
            var objects = new StarObjects();

            if (HasStarBase(starId))
            {
                objects.Add(StarObjectType.StarBase);
                return objects;
            }

            if (starId < 24 && _customStarObjects.TryGetValue(starId, out objects))
                return objects;

            var value = _random.RandomInt(starId, 1000);
            var faction = GetRegion(starId).Faction;

            if (value >= 100 && value < 125)
                objects.Add(StarObjectType.Wormhole);
            //if (value >= 150 && value < 175 && Faction > 0)
            //	pointsOfInterest.Add(PointOfInterest.Laboratory);
            if (value >= 200 && value < 300 && faction == Faction.Neutral)
            	objects.Add(StarObjectType.Event);
            if (value >= 300 && value < 325 && faction == Faction.Neutral)
                objects.Add(StarObjectType.Survival);
            if (value >= 350 && value < 375 && faction != Faction.Neutral)
                objects.Add(StarObjectType.Arena);
            if (value >= 400 && value < 450 && (faction != Faction.Neutral || value < 420))
                objects.Add(StarObjectType.Boss);
            if (value >= 450 && value < 475 && faction == Faction.Neutral)
                objects.Add(StarObjectType.Ruins);
            if (CurrencyExtensions.PremiumCurrencyAllowed)
                if (value >= 500 && value < 510)
                    objects.Add(StarObjectType.Military);
            if (value >= 550 && value < 570)
                objects.Add(StarObjectType.Challenge);
            if (value >= 600 && value < 650 && faction != Faction.Neutral)
                objects.Add(StarObjectType.Hive);
            if (value >= 700 && value < 720 && faction == Faction.Neutral)
                objects.Add(StarObjectType.BlackMarket);
            if (value >= 800 && value < 810 && _holidayManager.IsChristmas)
                objects.Add(StarObjectType.Xmas);

            return objects;
        }

        public bool HasStarBase(int starId)
        {
            int x, y;
			StarLayout.IdToPosition(starId, out x, out y);
            if (!RegionMap.IsHomeStar(x, y))
                return false;

            return GetRegion(starId).Id != Region.UnoccupiedRegionId;
        }

        public void CaptureBase(int starId)
        {
            _starBase.Attack(starId);
        }

		public Occupants.Facade GetOccupant(int starId) { return new Occupants.Facade(_occupants, starId); }
        public Boss.Facade GetBoss(int starId) { return new Boss.Facade(_boss, starId); }
        public Ruins.Facade GetRuins(int starId) { return new Ruins.Facade(_ruins, starId); }
        public XmasTree.Facade GetXmasTree(int starId) { return new XmasTree.Facade(_xmas, starId); }
        public Challenge.Facade GetChallenge(int starId) { return new Challenge.Facade(_challenge, starId); }
        public LocalEvent.Facade GetLocalEvent(int starId) { return new LocalEvent.Facade(_localEvent, starId); }
        public Survival.Facade GetSurvival(int starId) { return new Survival.Facade(_survival, starId); }
        public Wormhole.Facade GetWormhole(int starId) { return new Wormhole.Facade(_wormhole, starId); }
        public StarContent.Hive.Facade GetPandemic(int starId) { return new Hive.Facade(_hive, starId); }
        protected override void OnSessionDataLoaded()
        {
            _customStarObjects.Clear();

            var random = new Random(_session.Game.Seed);
            var stars = new Queue<int>(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 10, 11, 12, 14, 16, 17, 18, 21, 22, 23 }.OrderBy(id => random.Next()));

            _customStarObjects.Add(stars.Dequeue(), StarObjects.Create(StarObjectType.Ruins));
            _customStarObjects.Add(stars.Dequeue(), StarObjects.Create(StarObjectType.BlackMarket));
            _customStarObjects.Add(stars.Dequeue(), StarObjects.Create(StarObjectType.Challenge));
            _customStarObjects.Add(stars.Dequeue(), StarObjects.Create(StarObjectType.Boss));
            _customStarObjects.Add(stars.Dequeue(), StarObjects.Create(StarObjectType.Event));
            _customStarObjects.Add(stars.Dequeue(), StarObjects.Create(StarObjectType.Event));
            _customStarObjects.Add(stars.Dequeue(), StarObjects.Create(StarObjectType.Event));
            _customStarObjects.Add(stars.Dequeue(), StarObjects.Create(StarObjectType.Survival));
            _customStarObjects.Add(stars.Dequeue(), StarObjects.Create(StarObjectType.Wormhole));
            if (_holidayManager.IsChristmas)
                _customStarObjects.Add(stars.Dequeue(), StarObjects.Create(StarObjectType.Xmas));
#if UNITY_ANDROID
            // TODO: _customStarObjects.Add(stars.Dequeue(), StarObjects.Create(StarObjectType.Multiplayer));
#endif

            while (stars.Count > 0)
                _customStarObjects.Add(stars.Dequeue(), new StarObjects());

            //foreach (var item in _customStarObjects)
            //    _session.StarMap.SetEnemy(item.Key, StarMapData.Occupant.Empty);

            _session.StarMap.SetEnemy(0, StarMapData.Occupant.Empty);
            SetVisited(0);
        }

        protected override void OnSessionCreated()
        {
        }

        private readonly Dictionary<int, StarObjects> _customStarObjects = new Dictionary<int, StarObjects>();
        private readonly ISessionData _session;
    }

    public class StarContentChangedSignal : SmartWeakSignal<int> { public class Trigger : TriggerBase {} }
}
