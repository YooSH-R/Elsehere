using System.Linq;
using NUnit.Framework;

namespace A305.Onboarding.Tests
{
    public class SignupFormTests
    {
        private static readonly ConsentTerm Terms = Term("TERMS", required: true);
        private static readonly ConsentTerm Privacy = Term("PRIVACY", required: true);
        private static readonly ConsentTerm Voice = Term("VOICE", required: false);

        private SignupForm form;

        [SetUp]
        public void CreateForm()
        {
            form = new SignupForm(new[] { Terms, Privacy, Voice });
        }

        [Test]
        public void NewForm_CannotSubmit()
        {
            Assert.That(form.CanSubmit, Is.False);
            Assert.That(form.NicknameStatus, Is.EqualTo(NicknameStatus.Unchecked));
        }

        [Test]
        public void CheckedNicknameAvatarAndRequiredConsents_CanSubmit()
        {
            FillValidForm();

            Assert.That(form.CanSubmit, Is.True);
        }

        [Test]
        public void ChangingNicknameAfterCheck_NeedsCheckAgain()
        {
            FillValidForm();

            form.ChangeNickname("Moon");

            Assert.That(form.NicknameStatus, Is.EqualTo(NicknameStatus.Unchecked));
            Assert.That(form.CanSubmit, Is.False);
        }

        [Test]
        public void AvailabilityOfPreviousNickname_IsIgnored()
        {
            // 중복 확인 응답이 오기 전에 닉네임을 바꾸면 늦게 온 응답을 지금 닉네임에 적용하지 않는다.
            form.ChangeNickname("Sun");
            form.ChangeNickname("Moon");

            form.RecordAvailability("Sun", true);

            Assert.That(form.NicknameStatus, Is.EqualTo(NicknameStatus.Unchecked));
        }

        [Test]
        public void TakenNickname_CannotSubmit()
        {
            FillValidForm();

            form.RecordAvailability("Sun", false);

            Assert.That(form.NicknameStatus, Is.EqualTo(NicknameStatus.Taken));
            Assert.That(form.CanSubmit, Is.False);
        }

        [Test]
        public void MissingRequiredConsent_CannotSubmit()
        {
            FillValidForm();

            form.SetAgreed("PRIVACY", false);

            Assert.That(form.CanSubmit, Is.False);
        }

        [Test]
        public void ToRequest_SendsAgreedConsentsWithServerVersions()
        {
            FillValidForm();
            form.SetAgreed("VOICE", true);

            SignupRequest request = form.ToRequest();

            Assert.That(request.Nickname, Is.EqualTo("Sun"));
            Assert.That(request.AvatarType, Is.EqualTo(AvatarType.M));
            Assert.That(
                request.Consents.Select(consent => consent.ConsentType + "@" + consent.Version),
                Is.EquivalentTo(new[] { "TERMS@2026-10", "PRIVACY@2026-10", "VOICE@2026-10" })
            );
        }

        [Test]
        public void ToRequest_LeavesOutOptionalConsentNotAgreed()
        {
            FillValidForm();

            SignupRequest request = form.ToRequest();

            Assert.That(
                request.Consents.Select(consent => consent.ConsentType),
                Is.EquivalentTo(new[] { "TERMS", "PRIVACY" })
            );
        }

        // S15P31A305-596: 약관 동의 단계의 [전체 동의]와 [다음]
        [Test]
        public void AgreeAll_TurnsEveryTermOnAndOff()
        {
            form.AgreeAll(true);

            Assert.That(
                form.IsAgreed("TERMS") && form.IsAgreed("PRIVACY") && form.IsAgreed("VOICE")
            );
            Assert.That(form.IsAllAgreed, Is.True);

            form.AgreeAll(false);

            Assert.That(
                form.IsAgreed("TERMS") || form.IsAgreed("PRIVACY") || form.IsAgreed("VOICE"),
                Is.False
            );
            Assert.That(form.IsAllAgreed, Is.False);
        }

        [Test]
        public void TurningOneTermOff_ClearsAllAgreed()
        {
            form.AgreeAll(true);

            form.SetAgreed("VOICE", false);

            Assert.That(form.IsAllAgreed, Is.False);
        }

        [Test]
        public void CanContinueFromTerms_NeedsOnlyTheRequiredTerms()
        {
            Assert.That(form.CanContinueFromTerms, Is.False);

            form.SetAgreed("TERMS", true);
            Assert.That(form.CanContinueFromTerms, Is.False);

            form.SetAgreed("PRIVACY", true);
            Assert.That(form.CanContinueFromTerms, Is.True, "선택 약관은 없어도 넘어갑니다.");
        }

        [Test]
        public void GuestForm_CanSubmitWithoutChoosingCharacterAndSendsGuestCharacter()
        {
            var guestForm = new SignupForm(
                new[] { Terms, Privacy, Voice },
                new ConvertingGuest { AvatarType = AvatarType.M, VoiceConsent = false }
            );
            guestForm.ChangeNickname("Sun");
            guestForm.RecordAvailability("Sun", true);
            guestForm.SetAgreed("TERMS", true);
            guestForm.SetAgreed("PRIVACY", true);

            Assert.That(guestForm.IsGuestConversion, Is.True);
            Assert.That(guestForm.CanSubmit, Is.True);
            Assert.That(guestForm.ToRequest().AvatarType, Is.EqualTo(AvatarType.M));
        }

        [Test]
        public void GuestForm_StartsWithVoiceConsentTheGuestGave()
        {
            var guestForm = new SignupForm(
                new[] { Terms, Privacy, Voice },
                new ConvertingGuest { AvatarType = AvatarType.F, VoiceConsent = true }
            );

            Assert.That(guestForm.IsAgreed("VOICE"), Is.True);
        }

        [Test]
        public void MemberForm_IsNotGuestConversion()
        {
            Assert.That(form.IsGuestConversion, Is.False);
            Assert.That(form.Avatar, Is.Null);
        }

        private void FillValidForm()
        {
            form.ChangeNickname("Sun");
            form.RecordAvailability("Sun", true);
            form.SelectAvatar(AvatarType.M);
            form.SetAgreed("TERMS", true);
            form.SetAgreed("PRIVACY", true);
        }

        private static ConsentTerm Term(string consentType, bool required)
        {
            return new ConsentTerm
            {
                ConsentType = consentType,
                Version = "2026-10",
                Required = required,
                Title = consentType,
            };
        }
    }
}
