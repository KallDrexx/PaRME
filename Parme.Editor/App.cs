﻿using System;
using ImGuiHandler;
using ImGuiHandler.MonoGame;
using ImGuiNET;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Parme.Core;
using Parme.CSharp;
using Parme.CSharp.CodeGen;
using Parme.Editor.AppOperations;
using Parme.Editor.Ui;
using Parme.MonoGame;
using Vector2 = System.Numerics.Vector2;

namespace Parme.Editor
{
    public class App : Game
    {
        private const float MinSecondsForRecompilingEmitter = 0.25f;
        public const string DefaultExtension = ".emitter";
        
        private readonly ParticleCamera _camera = new ParticleCamera();
        private readonly ApplicationState _applicationState = new ApplicationState();
        private readonly AppOperationQueue _appOperationQueue;
        private readonly SettingsCommandHandler _commandHandler;
        private ITextureFileLoader _textureFileLoader;
        private MonoGameEmitter _emitter;
        private ImGuiManager _imGuiManager;
        private EditorUiController _uiController;
        private InputHandler _inputHandler;
        private float _lastProcessedEmitterChangeTime;

        private Texture2D _testTexture;
        
        public App()
        {
            _appOperationQueue = new AppOperationQueue();
            _commandHandler = new SettingsCommandHandler(_appOperationQueue);
            
            // ReSharper disable once ObjectCreationAsStatement
            new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 1024,
                PreferredBackBufferHeight = 768,
                PreferMultiSampling = true
            };

            IsMouseVisible = true;
            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += WindowOnClientSizeChanged;
        }

        protected override void Initialize()
        {
            _textureFileLoader = new TextureFileLoader(GraphicsDevice, _applicationState);
            
            _camera.Origin = new Vector2(-GraphicsDevice.Viewport.Width / 6f, GraphicsDevice.Viewport.Height / 4f);
            _camera.PositiveYAxisPointsUp = true;
            _camera.PixelWidth = GraphicsDevice.Viewport.Width;
            _camera.PixelHeight = GraphicsDevice.Viewport.Height;
            
            _imGuiManager = new ImGuiManager(new MonoGameImGuiRenderer(this));
            _uiController = new EditorUiController(_imGuiManager, _commandHandler, _appOperationQueue, _applicationState);
            _inputHandler = new InputHandler(_uiController, _camera, _commandHandler);

            ImGui.GetIO().FontGlobalScale = 1.2f;
            _uiController.WindowResized(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

            var pixels = new Color[10*10];
            for (var x = 0; x < pixels.Length; x++)
            {
                pixels[x] = Color.White;
            }
            
            _testTexture = new Texture2D(GraphicsDevice, 10, 10);
            _testTexture.SetData(pixels);

            base.Initialize();
        }

        protected override void Update(GameTime gameTime)
        {
            _applicationState.UpdateTotalTime((float) gameTime.TotalGameTime.TotalSeconds);

            while (_appOperationQueue.TryDequeue(out var appOperation))
            {
                var operationResult = appOperation.Run();
                _applicationState.Apply(operationResult);
            }

            // Only update the emitter if it's been updated since the last time we have processed it
            // and we have waited the debounce period
            if (Math.Abs(_lastProcessedEmitterChangeTime - _applicationState.TimeLastEmitterUpdated) > 0.0001f &&
                gameTime.TotalGameTime.TotalSeconds - _applicationState.TimeLastEmitterUpdated > MinSecondsForRecompilingEmitter)
            {
                var settings = _applicationState.ActiveEmitter;
                if (_applicationState.EmitterUpdatedFromFileLoad)
                {
                    _commandHandler.NewStartingEmitter(settings);
                }
                
                UpdateEmitter(settings);
                
                _lastProcessedEmitterChangeTime = _applicationState.TimeLastEmitterUpdated;
            }
            
            _commandHandler.UpdateTime((float) gameTime.ElapsedGameTime.TotalSeconds);
            _uiController.Update();
            _inputHandler.Update();
            _emitter?.Update((float) gameTime.ElapsedGameTime.TotalSeconds);
            
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            var backgroundColorVector = _uiController.BackgroundColor;
            var backgroundColor = new Color(backgroundColorVector.X, 
                backgroundColorVector.Y, 
                backgroundColorVector.Z);
            
            GraphicsDevice.Clear(backgroundColor);

            _emitter?.Render(_camera);
            _imGuiManager.RenderElements(gameTime.ElapsedGameTime);
            
            base.Draw(gameTime);
        }

        private void UpdateEmitter(EmitterSettings settings)
        {
            if (_emitter != null)
            {
                _emitter.Stop();
                _emitter.KillAllParticles();
                _emitter = null;
            }

            if (settings != null)
            {
                var code = EmitterLogicClassGenerator.Generate(settings, "Parme.Editor", "Test", true);
            
                var scriptOptions = ScriptOptions.Default
                    .WithReferences(typeof(IEmitterLogic).Assembly);
                
                var logicClass = CSharpScript.EvaluateAsync<IEmitterLogic>(code, scriptOptions).GetAwaiter().GetResult();

                _emitter = new MonoGameEmitter(logicClass, GraphicsDevice, _textureFileLoader)
                {
                    //WorldCoordinates = new System.Numerics.Vector2(400, -200)
                };
                _emitter.Start();
            }
        }

        private void WindowOnClientSizeChanged(object sender, EventArgs e)
        {
            _uiController.WindowResized(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            _camera.PixelWidth = GraphicsDevice.Viewport.Width;
            _camera.PixelHeight = GraphicsDevice.Viewport.Height;
        }
    }
}