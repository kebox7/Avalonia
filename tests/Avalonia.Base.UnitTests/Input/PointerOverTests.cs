﻿#nullable enable
using System;
using System.Collections.Generic;

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Avalonia.Rendering;
using Avalonia.UnitTests;

using Moq;

using Xunit;

namespace Avalonia.Base.UnitTests.Input
{
    public class PointerOverTests : PointerTestsBase
    {
        // https://github.com/AvaloniaUI/Avalonia/issues/2821
        [Fact]
        public void Close_Should_Remove_PointerOver()
        {
            using var app = UnitTestApplication.Start(new TestServices(
                inputManager: new InputManager(),
                focusManager: new FocusManager(),
                renderInterface: new HeadlessPlatformRenderInterface()));

            var renderer = new Mock<IHitTester>();
            var device = CreatePointerDeviceMock().Object;
            var impl = CreateTopLevelImplMock();

            Canvas canvas;
            var root = CreateInputRoot(impl.Object, new Panel
            {
                Children =
                {
                    (canvas = new Canvas())
                }
            }, renderer.Object);

            SetHit(renderer, canvas);
            impl.Object.Input!(CreateRawPointerMovedArgs(device, root));

            Assert.True(canvas.IsPointerOver);

            impl.Object.Closed!();

            Assert.False(canvas.IsPointerOver);
        }

        [Fact]
        public void MouseMove_Should_Update_IsPointerOver()
        {
            using var app = UnitTestApplication.Start(new TestServices(inputManager: new InputManager()));

            var renderer = new Mock<IHitTester>();
            var device = CreatePointerDeviceMock().Object;
            var impl = CreateTopLevelImplMock();

            Canvas canvas;
            Border border;
            Decorator decorator;

            var root = CreateInputRoot(impl.Object, new Panel
            {
                Children =
                {
                    (canvas = new Canvas()),
                    (border = new Border
                    {
                        Child = decorator = new Decorator(),
                    })
                }
            }, renderer.Object);

            SetHit(renderer, decorator);
            impl.Object.Input!(CreateRawPointerMovedArgs(device, root));

            Assert.True(decorator.IsPointerOver);
            Assert.True(border.IsPointerOver);
            Assert.False(canvas.IsPointerOver);
            Assert.True(root.IsPointerOver);

            SetHit(renderer, canvas);
            impl.Object.Input!(CreateRawPointerMovedArgs(device, root));

            Assert.False(decorator.IsPointerOver);
            Assert.False(border.IsPointerOver);
            Assert.True(canvas.IsPointerOver);
            Assert.True(root.IsPointerOver);
        }


        [Fact]
        public void TouchMove_Should_Not_Set_IsPointerOver()
        {
            using var app = UnitTestApplication.Start(new TestServices(inputManager: new InputManager()));

            var renderer = new Mock<IHitTester>();
            var device = CreatePointerDeviceMock(pointerType: PointerType.Touch).Object;
            var impl = CreateTopLevelImplMock();

            Canvas canvas;

            var root = CreateInputRoot(impl.Object, new Panel
            {
                Children =
                {
                    (canvas = new Canvas())
                }
            }, renderer.Object);

            SetHit(renderer, canvas);
            impl.Object.Input!(CreateRawPointerMovedArgs(device, root));

            Assert.False(canvas.IsPointerOver);
            Assert.False(root.IsPointerOver);
        }

        [Fact]
        public void HitTest_Should_Ignore_Non_Captured_Elements()
        {
            using var app = UnitTestApplication.Start(new TestServices(inputManager: new InputManager()));

            var renderer = new Mock<IHitTester>();
            var pointer = new Mock<IPointer>();
            var device = CreatePointerDeviceMock(pointer.Object).Object;
            var impl = CreateTopLevelImplMock();

            Canvas canvas;
            Border border;
            Decorator decorator;

            var root = CreateInputRoot(impl.Object, new Panel
            {
                Children =
                {
                    (canvas = new Canvas()),
                    (border = new Border
                    {
                        Child = decorator = new Decorator(),
                    })
                }
            }, renderer.Object);

            pointer.SetupGet(p => p.Captured).Returns(decorator);

            // Move the pointer over the canvas: the captured decorator should lose the pointer over state.
            SetHit(renderer, canvas);
            impl.Object.Input!(CreateRawPointerMovedArgs(device, root));

            Assert.False(decorator.IsPointerOver);
            Assert.False(border.IsPointerOver);
            Assert.False(canvas.IsPointerOver);
            Assert.False(root.IsPointerOver);

            // Move back the pointer over the decorator: raise events normally for it since it's captured.
            SetHit(renderer, decorator);
            impl.Object.Input!(CreateRawPointerMovedArgs(device, root));

            Assert.True(decorator.IsPointerOver);
            Assert.True(border.IsPointerOver);
            Assert.False(canvas.IsPointerOver);
            Assert.True(root.IsPointerOver);
        }

        [Fact]
        public void IsPointerOver_Should_Be_Updated_When_Child_Sets_Handled_True()
        {
            using var app = UnitTestApplication.Start(new TestServices(inputManager: new InputManager()));

            var renderer = new Mock<IHitTester>();
            var device = CreatePointerDeviceMock().Object;
            var impl = CreateTopLevelImplMock();

            Canvas canvas;
            Border border;
            Decorator decorator;

            var root = CreateInputRoot(impl.Object, new Panel
            {
                Children =
                {
                    (canvas = new Canvas()),
                    (border = new Border
                    {
                        Child = decorator = new Decorator(),
                    })
                }
            }, renderer.Object);

            SetHit(renderer, canvas);
            impl.Object.Input!(CreateRawPointerMovedArgs(device, root));

            Assert.False(decorator.IsPointerOver);
            Assert.False(border.IsPointerOver);
            Assert.True(canvas.IsPointerOver);
            Assert.True(root.IsPointerOver);

            // Ensure that e.Handled is reset between controls.
            root.PointerMoved += (s, e) => e.Handled = true;
            decorator.PointerEntered += (s, e) => e.Handled = true;

            SetHit(renderer, decorator);
            impl.Object.Input!(CreateRawPointerMovedArgs(device, root));

            Assert.True(decorator.IsPointerOver);
            Assert.True(border.IsPointerOver);
            Assert.False(canvas.IsPointerOver);
            Assert.True(root.IsPointerOver);
        }

        [Fact]
        public void Pointer_Enter_Move_Leave_Should_Be_Followed()
        {
            using var app = UnitTestApplication.Start(new TestServices(inputManager: new InputManager()));

            var renderer = new Mock<IHitTester>();
            var deviceMock = CreatePointerDeviceMock();
            var impl = CreateTopLevelImplMock();
            var result = new List<(object?, string)>();

            void HandleEvent(object? sender, PointerEventArgs e)
            {
                result.Add((sender, e.RoutedEvent!.Name));
            }

            Canvas canvas;
            Border border;
            Decorator decorator;

            var root = CreateInputRoot(impl.Object, new Panel
            {
                Children =
                {
                    (canvas = new Canvas()),
                    (border = new Border
                    {
                        Child = decorator = new Decorator(),
                    })
                }
            }, renderer.Object);

            AddEnteredExitedHandlers(HandleEvent, canvas, decorator);

            // Enter decorator
            SetHit(renderer, decorator);
            SetMove(deviceMock, root, decorator);
            impl.Object.Input!(CreateRawPointerMovedArgs(deviceMock.Object, root));

            // Leave decorator
            SetHit(renderer, canvas);
            SetMove(deviceMock, root, canvas);
            impl.Object.Input!(CreateRawPointerMovedArgs(deviceMock.Object, root));

            Assert.Equal(
                new[]
                {
                        ((object?)decorator, nameof(InputElement.PointerEntered)),
                        (decorator, nameof(InputElement.PointerMoved)),
                        (decorator, nameof(InputElement.PointerExited)),
                        (canvas, nameof(InputElement.PointerEntered)),
                        (canvas, nameof(InputElement.PointerMoved))
                },
                result);
        }

        [Fact]
        public void PointerEntered_Exited_Should_Be_Raised_In_Correct_Order()
        {
            using var app = UnitTestApplication.Start(new TestServices(inputManager: new InputManager()));

            var renderer = new Mock<IHitTester>();
            var deviceMock = CreatePointerDeviceMock();
            var impl = CreateTopLevelImplMock();
            var result = new List<(object?, string)>();

            void HandleEvent(object? sender, PointerEventArgs e)
            {
                result.Add((sender, e.RoutedEvent!.Name));
            }

            Canvas canvas;
            Border border;
            Decorator decorator;

            var root = CreateInputRoot(impl.Object, new Panel
            {
                Children =
                {
                    (canvas = new Canvas()),
                    (border = new Border
                    {
                        Child = decorator = new Decorator(),
                    })
                }
            }, renderer.Object);

            SetHit(renderer, canvas);
            impl.Object.Input!(CreateRawPointerMovedArgs(deviceMock.Object, root));

            AddEnteredExitedHandlers(HandleEvent, root, canvas, border, decorator);

            SetHit(renderer, decorator);
            impl.Object.Input!(CreateRawPointerMovedArgs(deviceMock.Object, root));

            Assert.Equal(
                new[]
                {
                    ((object?)canvas, nameof(InputElement.PointerExited)),
                    (decorator, nameof(InputElement.PointerEntered)),
                    (border, nameof(InputElement.PointerEntered)),
                },
                result);
        }

        // https://github.com/AvaloniaUI/Avalonia/issues/7896
        [Fact]
        public void PointerEntered_Exited_Should_Set_Correct_Position()
        {
            using var app = UnitTestApplication.Start(new TestServices(inputManager: new InputManager()));

            var expectedPosition = new Point(15, 15);
            var renderer = new Mock<IHitTester>();
            var deviceMock = CreatePointerDeviceMock();
            var impl = CreateTopLevelImplMock();
            var result = new List<(object?, string, Point)>();

            void HandleEvent(object? sender, PointerEventArgs e)
            {
                result.Add((sender, e.RoutedEvent!.Name, e.GetPosition(null)));
            }

            Canvas canvas;

            var root = CreateInputRoot(impl.Object, new Panel
            {
                Children =
                {
                    (canvas = new Canvas())
                }
            }, renderer.Object);

            AddEnteredExitedHandlers(HandleEvent, root, canvas);

            SetHit(renderer, canvas);
            impl.Object.Input!(CreateRawPointerMovedArgs(deviceMock.Object, root, expectedPosition));

            SetHit(renderer, null);
            impl.Object.Input!(CreateRawPointerMovedArgs(deviceMock.Object, root, expectedPosition));

            Assert.Equal(
                new[]
                {
                    ((object?)canvas, nameof(InputElement.PointerEntered), expectedPosition),
                    (root, nameof(InputElement.PointerEntered), expectedPosition),
                    (canvas, nameof(InputElement.PointerExited), expectedPosition),
                    (root, nameof(InputElement.PointerExited), expectedPosition)
                },
                result);
        }

        void RaiseSceneInvalidated(TopLevel tl) =>
            tl.Renderer.TriggerSceneInvalidatedForUnitTests(new Rect(0, 0, 10000, 10000));

        [Fact]
        public void Render_Invalidation_Should_Affect_PointerOver()
        {
            using var app = UnitTestApplication.Start(new TestServices(inputManager: new InputManager()));

            var renderer = new Mock<IHitTester>();
            var deviceMock = CreatePointerDeviceMock();
            var impl = CreateTopLevelImplMock();

            var invalidateRect = new Rect(0, 0, 15, 15);
            var lastClientPosition = new Point(1, 5);

            var result = new List<(object?, string, Point)>();
            void HandleEvent(object? sender, PointerEventArgs e)
            {
                result.Add((sender, e.RoutedEvent!.Name, e.GetPosition(null)));
            }

            Canvas canvas;

            var root = (Window)CreateInputRoot(impl.Object, new Panel
            {
                Children =
                {
                    (canvas = new Canvas())
                }
            }, renderer.Object);
            AddEnteredExitedHandlers(HandleEvent, root, canvas);

            // Let input know about latest device.
            SetHit(renderer, canvas);
            impl.Object.Input!(CreateRawPointerMovedArgs(deviceMock.Object, root, lastClientPosition));
            Assert.True(canvas.IsPointerOver);

            SetHit(renderer, canvas);
            RaiseSceneInvalidated(root);
            Assert.True(canvas.IsPointerOver);

            // Raise SceneInvalidated again, but now hide element from the hittest.
            SetHit(renderer, null);
            RaiseSceneInvalidated(root);
            Assert.False(canvas.IsPointerOver);

            Assert.Equal(
                new[]
                {
                    ((object?)canvas, nameof(InputElement.PointerEntered), lastClientPosition),
                    (root, nameof(InputElement.PointerEntered), lastClientPosition),
                    (canvas, nameof(InputElement.PointerExited), lastClientPosition),
                    (root, nameof(InputElement.PointerExited), lastClientPosition),
                },
                result);
        }

        [Fact]
        public void PointerOver_Invalidation_Should_Use_Previously_Captured_Element()
        {
            using var app = UnitTestApplication.Start(new TestServices(inputManager: new InputManager()));

            var renderer = new Mock<IHitTester>();
            var deviceMock = CreatePointerDeviceMock();
            var impl = CreateTopLevelImplMock();

            var invalidateRect = new Rect(0, 0, 15, 15);

            Canvas canvas1, canvas2;

            var root = CreateInputRoot(impl.Object, new Panel
            {
                Children =
                {
                    (canvas1 = new Canvas()),
                    (canvas2 = new Canvas())
                }
            }, renderer.Object);

            canvas1.PointerMoved += (s, a) => a.Pointer.Capture(canvas1);

            // Let input know about latest device.
            SetHit(renderer, canvas1);
            impl.Object.Input!(CreateRawPointerMovedArgs(deviceMock.Object, root));
            Assert.True(canvas1.IsPointerOver);
            Assert.False(canvas2.IsPointerOver);

            SetHit(renderer, canvas2);
            RaiseSceneInvalidated(root);
            Assert.False(canvas1.IsPointerOver);
            Assert.True(canvas2.IsPointerOver);
        }

        // https://github.com/AvaloniaUI/Avalonia/issues/7748
        [Fact]
        public void LeaveWindow_Should_Reset_PointerOver()
        {
            using var app = UnitTestApplication.Start(new TestServices(inputManager: new InputManager()));

            var renderer = new Mock<IHitTester>();
            var deviceMock = CreatePointerDeviceMock();
            var impl = CreateTopLevelImplMock();

            var lastClientPosition = new Point(1, 5);
            var invalidateRect = new Rect(0, 0, 15, 15);
            var result = new List<(object?, string, Point)>();

            void HandleEvent(object? sender, PointerEventArgs e)
            {
                result.Add((sender, e.RoutedEvent!.Name, e.GetPosition(null)));
            }

            Canvas canvas;

            var root = CreateInputRoot(impl.Object, new Panel
            {
                Children =
                {
                    (canvas = new Canvas())
                }
            }, renderer.Object);

            AddEnteredExitedHandlers(HandleEvent, root, canvas);

            // Init pointer over.
            SetHit(renderer, canvas);
            impl.Object.Input!(CreateRawPointerMovedArgs(deviceMock.Object, root, lastClientPosition));
            Assert.True(canvas.IsPointerOver);

            // Send LeaveWindow.
            impl.Object.Input!(new RawPointerEventArgs(deviceMock.Object, 0, root, RawPointerEventType.LeaveWindow, new Point(), default));
            Assert.False(canvas.IsPointerOver);

            Assert.Equal(
                new[]
                {
                    ((object?)canvas, nameof(InputElement.PointerEntered), lastClientPosition),
                    (root, nameof(InputElement.PointerEntered), lastClientPosition),
                    (canvas, nameof(InputElement.PointerExited), lastClientPosition),
                    (root, nameof(InputElement.PointerExited), lastClientPosition),
                },
                result);
        }

        [Fact]
        public void Disabled_Element_Should_Set_PointerOver_On_Visual_Parent()
        {
            using var app = UnitTestApplication.Start(new TestServices(inputManager: new InputManager()));

            var renderer = new Mock<IHitTester>();
            var deviceMock = CreatePointerDeviceMock();
            var impl = CreateTopLevelImplMock();

            var disabledChild = new Border
            {
                Background = Brushes.Red,
                Width = 100,
                Height = 100,
                IsEnabled = false
            };

            var visualParent = new Border
            {
                Background = Brushes.Black,
                Width = 100,
                Height = 100,
                Child = disabledChild
            };

            var logicalParent = new Border
            {
                Background = Brushes.Blue,
                Width = 100,
                Height = 100
            };

            // Change the logical parent and check that we're correctly hit testing on the visual tree.
            // This scenario is made up because it's easy to test.
            // In the real world, this happens with nested Popups from MenuItems (but that's very cumbersome to test).
            ((ISetLogicalParent) disabledChild).SetParent(null);
            ((ISetLogicalParent) disabledChild).SetParent(logicalParent);

            var root = CreateInputRoot(
                impl.Object,
                new Panel
                {
                    Children = { visualParent }
                },
                renderer.Object);

            Assert.False(visualParent.IsPointerOver);
            SetHit(renderer, disabledChild);

            impl.Object.Input!(CreateRawPointerMovedArgs(deviceMock.Object, root, new Point(50, 50)));
            Assert.True(visualParent.IsPointerOver);
            Assert.False(logicalParent.IsPointerOver);
        }

        private static void AddEnteredExitedHandlers(
            EventHandler<PointerEventArgs> handler,
            params IInputElement[] controls)
        {
            foreach (var c in controls)
            {
                c.PointerEntered += handler;
                c.PointerExited += handler;
                c.PointerMoved += handler;
            }
        }
    }
}
