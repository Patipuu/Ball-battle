using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FishNet.Component.Spawning;
using FishNet.Managing;
using FishNet.Managing.Observing;
using FishNet.Managing.Transporting;
using FishNet.Observing;
using FishNet.Transporting.FishyEOSPlugin;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using NUnit.Framework;
using TeamNet.Multiplayer.FishNetEos;
using UnityEditor;
using UnityEngine;

namespace TeamNet.Multiplayer.Wiring.Tests
{
    /// <summary>
    /// EditMode check that the NetworkManager prefab is wired the way the lobby/transport stack assumes.
    /// A wrong wiring does not fail loudly: a swapped Multipass order sends host traffic to the wrong
    /// transport, FishyEOS auto-authenticate fights the boot login, a missing observer condition lets
    /// a slow client receive spawns for a scene it has not loaded, and FishNet's PlayerSpawner spawns
    /// players before the load barrier.
    ///
    /// Usage: derive once in the game and point <see cref="NetworkManagerPrefabPath"/> at the prefab:
    ///   public sealed class MyWiringTests : NetworkManagerWiringTests
    ///   { protected override string NetworkManagerPrefabPath => "Assets/Prefabs/NetworkManager.prefab"; }
    /// The base class is abstract so it does not run (and fail) in a project that has not chosen a path.
    ///
    /// Why reflection: ObserverManager's default conditions list is a private [SerializeField]
    /// (`_defaultConditions`); TransportManager.Transport, Multipass.Transports and
    /// FishyEOS.AutoAuthenticate are public. A prefab is not initialized, so NetworkManager.TransportManager
    /// is null here: the managers are fetched with GetComponent. If a FishNet upgrade renames
    /// `_defaultConditions` the test fails with a clear message instead of passing silently.
    /// Status: compiled only; not run against a real prefab in this skill.
    /// </summary>
    public abstract class NetworkManagerWiringTests
    {
        /// <summary>Asset path of the prefab holding the NetworkManager (with TransportManager and ObserverManager).</summary>
        protected abstract string NetworkManagerPrefabPath { get; }

        // Multipass order the stack assumes: index 0 LAN/direct, index 1 EOS relay.
        protected virtual int TugboatIndex => 0;
        protected virtual int FishyEosIndex => 1;

        GameObject root;

        [OneTimeSetUp]
        public void LoadPrefab()
        {
            root = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkManagerPrefabPath);
            Assert.IsNotNull(root, $"No prefab at '{NetworkManagerPrefabPath}'.");
            Assert.IsNotNull(root.GetComponentInChildren<NetworkManager>(true),
                $"'{NetworkManagerPrefabPath}' has no NetworkManager component.");
        }

        T Get<T>() where T : Component
        {
            var c = root.GetComponentInChildren<T>(true);
            Assert.IsNotNull(c, $"{typeof(T).Name} missing on '{NetworkManagerPrefabPath}'.");
            return c;
        }

        Multipass GetMultipass()
        {
            var transport = Get<TransportManager>().Transport;
            Assert.IsNotNull(transport, "TransportManager.Transport is not assigned.");
            Assert.IsInstanceOf<Multipass>(transport,
                $"TransportManager.Transport must be Multipass, found {transport.GetType().Name}.");
            return (Multipass)transport;
        }

        [Test]
        public void ActiveTransport_IsMultipass() => GetMultipass();

        [Test]
        public void Multipass_Slot0_IsTugboat_Slot1_IsFishyEos()
        {
            var transports = GetMultipass().Transports;
            Assert.Greater(transports.Count, System.Math.Max(TugboatIndex, FishyEosIndex),
                $"Multipass has only {transports.Count} transports.");
            Assert.IsInstanceOf<Tugboat>(transports[TugboatIndex], $"Multipass[{TugboatIndex}] must be Tugboat.");
            Assert.IsInstanceOf<FishyEOS>(transports[FishyEosIndex], $"Multipass[{FishyEosIndex}] must be FishyEOS.");
        }

        [Test]
        public void FishyEos_AutoAuthenticate_IsOff()
        {
            var eos = GetMultipass().Transports.OfType<FishyEOS>().FirstOrDefault();
            Assert.IsNotNull(eos, "No FishyEOS in Multipass.Transports.");
            Assert.IsFalse(eos.AutoAuthenticate,
                "FishyEOS autoAuthenticate must be false: login is owned by the boot task (EosBootstrap).");
        }

        [Test]
        public void ObserverManager_DefaultConditions_ContainMatchLoadCondition()
        {
            var field = typeof(ObserverManager).GetField("_defaultConditions",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "ObserverManager._defaultConditions not found; FishNet changed, update this test.");
            var list = field.GetValue(Get<ObserverManager>()) as List<ObserverCondition>;
            Assert.IsNotNull(list, "ObserverManager._defaultConditions has an unexpected type.");
            Assert.IsTrue(list.Any(c => c is MatchLoadObserverCondition),
                "ObserverManager default conditions must contain a MatchLoadObserverCondition asset.");
        }

        [Test]
        public void NoFishNetPlayerSpawner_Present()
        {
            Assert.IsEmpty(root.GetComponentsInChildren<PlayerSpawner>(true),
                "FishNet PlayerSpawner must be removed: players are spawned by PlayerSpawnServiceTemplate after the load barrier.");
        }
    }
}
