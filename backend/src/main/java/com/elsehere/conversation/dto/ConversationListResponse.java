package com.elsehere.conversation.dto;

import java.util.List;

/**
 * 지난 대화 목록(CONV-09) 한 페이지.
 *
 * @param nextCursor 다음 페이지를 요청할 때 보내는 값. 마지막 페이지면 null이다.
 */
public record ConversationListResponse(
    List<ConversationSummaryResponse> items, String nextCursor) {}
