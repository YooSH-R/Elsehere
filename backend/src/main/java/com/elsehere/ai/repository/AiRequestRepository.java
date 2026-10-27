package com.elsehere.ai.repository;

import com.elsehere.ai.entity.AiRequest;
import com.elsehere.ai.entity.AiRequestKind;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;

public interface AiRequestRepository extends JpaRepository<AiRequest, Long> {

  Optional<AiRequest> findByRequestIdAndKind(UUID requestId, AiRequestKind kind);
}
