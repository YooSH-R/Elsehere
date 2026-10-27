package com.elsehere.memory.repository;

import com.elsehere.memory.entity.UserNpcRelation;
import com.elsehere.memory.entity.UserNpcRelationId;
import org.springframework.data.jpa.repository.JpaRepository;

public interface UserNpcRelationRepository
    extends JpaRepository<UserNpcRelation, UserNpcRelationId> {}
