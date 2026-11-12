using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace A305.ObjectHover.Tests
{
    /// <summary>
    /// 실제 장치 대신 Input System 시험 장치로 누르고 떼어, 이번 프레임의 누름·뗌을 빠짐없이 읽는지 확인한다.
    /// 휴대폰에서는 처음 닿은 손가락만이 아니라 손가락마다 읽고, 한 프레임 안에 누르고 뗀 것도 읽어야 한다.
    /// </summary>
    public class PointerChangesTests : InputTestFixture
    {
        private static readonly Vector2 Joystick = new Vector2(150f, 150f);
        private static readonly Vector2 Npc = new Vector2(900f, 500f);
        private readonly List<PointerChange> changes = new List<PointerChange>();

        [Test]
        public void MouseClickedWithinOneFrame_ReadsPressThenRelease()
        {
            Mouse mouse = AddCurrentDevice<Mouse>();
            Set(mouse.position, Npc);
            PointerChanges.Read(changes);

            // 화면이 잠깐 멈춘 사이에 누르고 뗀 것이 한 프레임에 같이 들어온다.
            Press(mouse.leftButton, queueEventOnly: true);
            Release(mouse.leftButton, queueEventOnly: true);
            InputSystem.Update();
            PointerChanges.Read(changes);

            Assert.That(
                Described(changes),
                Is.EqualTo(new[] { "0 누름 (900, 500)", "0 뗌 (900, 500)" })
            );
        }

        [Test]
        public void SecondFingerTapped_WhileThumbHoldsJoystick_ReadsSecondFinger()
        {
            AddCurrentDevice<Touchscreen>();

            BeginTouch(1, Joystick);
            PointerChanges.Read(changes);
            Assert.That(Described(changes), Is.EqualTo(new[] { "1 누름 (150, 150)" }));

            BeginTouch(2, Npc);
            PointerChanges.Read(changes);
            Assert.That(Described(changes), Is.EqualTo(new[] { "2 누름 (900, 500)" }));

            EndTouch(2, Npc);
            PointerChanges.Read(changes);
            Assert.That(Described(changes), Is.EqualTo(new[] { "2 뗌 (900, 500)" }));
        }

        [Test]
        public void FingerTappedWithinOneFrame_ReadsPressWhereItTouchedThenRelease()
        {
            AddCurrentDevice<Touchscreen>();

            BeginTouch(1, Npc, queueEventOnly: true);
            EndTouch(1, Npc + new Vector2(3f, 2f), queueEventOnly: true);
            InputSystem.Update();
            PointerChanges.Read(changes);

            Assert.That(
                Described(changes),
                Is.EqualTo(new[] { "1 누름 (900, 500)", "1 뗌 (903, 502)" })
            );
        }

        [Test]
        public void NothingPressedOrReleased_ClearsLastFrame()
        {
            AddCurrentDevice<Touchscreen>();
            BeginTouch(1, Npc);
            PointerChanges.Read(changes);

            InputSystem.Update();
            PointerChanges.Read(changes);

            Assert.That(changes, Is.Empty);
        }

        // 게임은 매 프레임 읽으므로 누르기 전에도 한 번 읽어 둔다.
        private T AddCurrentDevice<T>()
            where T : Pointer
        {
            T device = InputSystem.AddDevice<T>();
            device.MakeCurrent();
            PointerChanges.Read(changes);
            return device;
        }

        private static string[] Described(IEnumerable<PointerChange> read) =>
            read.Select(change =>
                    $"{change.PointerId} {(change.IsPress ? "누름" : "뗌")} "
                    + $"({change.Position.x:0}, {change.Position.y:0})"
                )
                .ToArray();
    }
}
