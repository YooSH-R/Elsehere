package com.elsehere.ai.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatCode;
import static org.mockito.BDDMockito.given;
import static org.mockito.Mockito.mock;

import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.user.entity.User;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

class GuestConversationLimitTest {

  private final ConversationRepository conversations = mock(ConversationRepository.class);

  @Test
  @DisplayName("제한값이 0이면 게스트도 회원처럼 대화 횟수 제한이 없다")
  void zeroLimit_guestHasNoConversationLimit() {
    User guest = mock(User.class);
    given(guest.isGuest()).willReturn(true);
    given(guest.getId()).willReturn(7L);
    given(conversations.countExchangedByUserId(7L)).willReturn(10L);
    GuestConversationLimit limit = new GuestConversationLimit(conversations, 0);

    assertThat(limit.remaining(guest)).isNull();
    assertThat(limit.isReached(guest)).isFalse();
    assertThatCode(() -> limit.check(guest)).doesNotThrowAnyException();
  }
}
