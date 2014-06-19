﻿using System;
using System.Linq.Expressions;
using BEPUphysics.UpdateableSystems.ForceFields;
using ComponentBind;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Xna.Framework;
using System.Reflection;
using System.IO.IsolatedStorage;
using Lemma.Components;
using System.Xml.Serialization;
using Lemma.Factories;
using Lemma.Util;
using ICSharpCode.SharpZipLib.GZip;

namespace Lemma.IO
{
	public class MapLoader
	{
		public const string MapDirectory = "Content\\Game";
		public const string MapExtension = "map";

		private static string[] persistentTypes = new[] { "PlayerData", };

		private static bool entityIsPersistent(Entity entity)
		{
			return MapLoader.persistentTypes.Contains(entity.Type);
		}

		public static Type[] IncludedTypes = new[]
		{
			typeof(PlayerTrigger),
			typeof(Trigger),
			typeof(PlayerCylinderTrigger),
			typeof(Voxel),
			typeof(DynamicVoxel),
			typeof(PlayerSpawn),
			typeof(Timer),
			typeof(Model),
			typeof(AnimatedModel),
			typeof(Water),
			typeof(TextElement),
			typeof(AmbientLight),
			typeof(DirectionalLight),
			typeof(PointLight),
			typeof(SpotLight),
			typeof(Script),
			typeof(ModelInstance),
			typeof(ModelInstance.ModelInstanceSystem),
			typeof(Zone),
			typeof(PhysicsBlock),
			typeof(Player),
			typeof(ParticleEmitter),
			typeof(PhysicsSphere),
			typeof(ModelAlpha),
			typeof(EnemyBase),
			typeof(Agent),
			typeof(AI),
			typeof(PID),
			typeof(PID3),
			typeof(RespawnLocation),
			typeof(ListProperty<RespawnLocation>),
			typeof(Property<RespawnLocation>),
			typeof(VoxelChaseAI),
			typeof(Phone),
			typeof(RaycastAI),
			typeof(SignalTower),
			typeof(Property<int>),
			typeof(Property<int>),
			typeof(Property<float>),
			typeof(Property<float>),
			typeof(Property<Direction>),
			typeof(Property<Direction>),
			typeof(Property<UInt32>),
			typeof(Property<UInt32>),
			typeof(Propagator),
			typeof(RotationController),
			typeof(PlayerData),
			typeof(Rift),
			typeof(Collectible),
			typeof(CameraStop),
			typeof(AmbientSound),
			typeof(Counter),
			typeof(Ticker),
			typeof(Setter),
			typeof(World),
			typeof(Note),
			typeof(VoxelAttachable),
			typeof(Cloud),
			typeof(DialogueFile),
			typeof(EffectBlock),
			typeof(EvilBlocks),
			typeof(SceneryBlock),
			typeof(FallingTower),
			typeof(Joint),
			typeof(Slider),
			typeof(Spinner),
			typeof(Levitator),
			typeof(RaycastAIMovement),
			typeof(Orb),
			typeof(MapExit),
			typeof(ImplodeBlock),
			typeof(TimeTrial),
			typeof(Rain),
			typeof(Rotator),
			typeof(Skybox),
			typeof(Smoke),
			typeof(Snake),
			typeof(Switch),
			typeof(Turret),
			typeof(VoxelFill),
			typeof(Data),
			typeof(Starter),
			typeof(Explosion),
		};

		public static XmlSerializer Serializer;

		static MapLoader()
		{
			MapLoader.Serializer = new XmlSerializer(typeof(List<Entity>), MapLoader.IncludedTypes);
		}

		public static void Load(Main main, string directory, string filename, bool deleteEditor = true)
		{
			main.LoadingMap.Execute(filename);

			// HACK HACK HACK
			main.MapFile.InternalValue = filename;

			if (directory == null)
				filename = Path.Combine(MapLoader.MapDirectory, filename);
			else
				filename = Path.Combine(directory, filename);

			if (!filename.EndsWith("." + MapLoader.MapExtension))
				filename += "." + MapLoader.MapExtension;

			//Need to test to see if we can load this using gzip.
			//If we can't, it's an old map, so load it using the straight filesystem.
			//Need to do this so that we don't break dudes' maps. Can remove in a short amount of time after pushed to Steam.
			using (Stream fs = File.OpenRead(filename))
			{
				Stream stream;
				GZipInputStream gzip = null;
				try
				{
					gzip = new GZipInputStream(File.OpenRead(filename));
					gzip.ReadByte();
					gzip.Close();
					//No exception, so we're good.

					stream = new GZipInputStream(fs);
				}
				catch (GZipException)
				{
					stream = fs;
				}
				finally
				{
					if (gzip != null && gzip.CanRead) gzip.Close();
				}
				MapLoader.Load(main, stream, deleteEditor);
				stream.Close();
			}
		}

		public static void New(Main main, string filename)
		{
			main.LoadingMap.Execute(filename);

			// HACK HACK HACK
			main.MapFile.InternalValue = filename;

			if (!filename.EndsWith("." + MapLoader.MapExtension))
				filename += "." + MapLoader.MapExtension;

			main.ClearEntities(false);

			// Create a new map
			Entity world = Factory.Get<WorldFactory>().CreateAndBind(main);
			world.Get<Transform>().Position.Value = new Vector3(0, 3, 0);
			main.Add(world);

			Entity ambientLight = Factory.Get<AmbientLightFactory>().CreateAndBind(main);
			ambientLight.Get<Transform>().Position.Value = new Vector3(0, 5.0f, 0);
			ambientLight.Get<AmbientLight>().Color.Value = new Vector3(0.25f, 0.25f, 0.25f);
			main.Add(ambientLight);

			Entity map = Factory.Get<VoxelFactory>().CreateAndBind(main);
			map.Get<Transform>().Position.Value = new Vector3(0, 1, 0);
			main.Add(map);

			main.MapLoaded.Execute();
		}

		private static void Load(Main main, Stream stream, bool deleteEditor = true)
		{
			main.Camera.Position.Value = new Vector3(0, -10000, 0);
			main.IsLoadingMap = true;
			main.ClearEntities(deleteEditor);

			List<Entity> entities = null;
			try
			{
				entities = (List<Entity>)MapLoader.Serializer.Deserialize(stream);
			}
			catch (InvalidOperationException e)
			{
				throw new Exception("Failed to deserialize file stream.", e);
			}

			foreach (Entity entity in entities)
			{
				Factory<Main> factory = Factory<Main>.Get(entity.Type);
				if (factory != null)
				{
					factory.Bind(entity, main);
					main.Add(entity);
				}
			}

			main.IsLoadingMap = false;
			main.MapLoaded.Execute();
		}

		public static void Save(Main main, string directory, string filename)
		{
			if (directory == null)
				filename = Path.Combine(MapLoader.MapDirectory, filename);
			else
				filename = Path.Combine(directory, Path.GetFileName(filename));

			if (!filename.EndsWith("." + MapLoader.MapExtension))
				filename += "." + MapLoader.MapExtension;

			try
			{
				using (MemoryStream ms = new MemoryStream())
				{
					MapLoader.Save(main, ms);
					ms.Seek(0, SeekOrigin.Begin);
					using (Stream fs = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None))
					using (Stream stream = new GZipOutputStream(fs))
						ms.CopyTo(stream);
				}
			}
			catch (Exception e)
			{
				throw new Exception("Failed to save map.", e);
			}
		}

		public static void Save(Main main, Stream stream)
		{
			MapLoader.Serializer.Serialize(stream, main.Entities.Where(x => x.Serialize && x.Active).ToList());
		}

		public static void Transition(Main main, string nextMap, string spawn = null)
		{
			Container notification = new Container();
			TextElement notificationText = new TextElement();

			Stream stream = new MemoryStream();

			Animation anim = new Animation
			(
				main.Spawner.FlashAnimation(),
				new Animation.Execute(delegate()
				{
					notification.Tint.Value = Microsoft.Xna.Framework.Color.Black;
					notification.Opacity.Value = 0.5f;
					notificationText.Name.Value = "Text";
					notificationText.FontFile.Value = "Font";
					notificationText.Text.Value = "Loading...";
					notification.Children.Add(notificationText);
					main.UI.Root.GetChildByName("Notifications").Children.Add(notification);
				}),
				new Animation.Delay(0.01f),
				new Animation.Execute(delegate()
				{
					// We are exiting the map; just save the state of the map without the player.
					ListProperty<RespawnLocation> respawnLocations = PlayerDataFactory.Instance.Get<PlayerData>().RespawnLocations;
					respawnLocations.Clear();

					List<Entity> persistentEntities = main.Entities.Where((Func<Entity, bool>)MapLoader.entityIsPersistent).ToList();

					IO.MapLoader.Serializer.Serialize(stream, persistentEntities);

					foreach (Entity e in persistentEntities)
						e.Delete.Execute();

					main.Spawner.StartSpawnPoint.Value = spawn;

					if (PlayerFactory.Instance != null)
						PlayerFactory.Instance.Delete.Execute();

					main.SaveCurrentMap();
					main.MapFile.Value = nextMap;

					notification.Visible.Value = false;
					stream.Seek(0, SeekOrigin.Begin);
					List<Entity> entities = (List<Entity>)IO.MapLoader.Serializer.Deserialize(stream);
					foreach (Entity e in entities)
					{
						Factory<Main> factory = Factory<Main>.Get(e.Type);
						factory.Bind(e, main);
						main.Add(e);
					}
					stream.Dispose();
				}),
				new Animation.Delay(1.5f),
				new Animation.Execute(delegate()
				{
					main.Screenshot.Take(main.ScreenSize);
				}),
				new Animation.Delay(0.01f),
				new Animation.Set<string>(notificationText.Text, "Saving..."),
				new Animation.Set<bool>(notification.Visible, true),
				new Animation.Delay(0.01f),
				new Animation.Execute(delegate()
				{
					main.SaveOverwrite();
				}),
				new Animation.Set<string>(notificationText.Text, "Saved"),
				new Animation.Parallel
				(
					new Animation.FloatMoveTo(notification.Opacity, 0.0f, 1.0f),
					new Animation.FloatMoveTo(notificationText.Opacity, 0.0f, 1.0f)
				),
				new Animation.Execute(notification.Delete)
			);
			anim.EnabledWhenPaused = false;
			main.AddComponent(anim);
		}
	}
}