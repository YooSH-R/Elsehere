package com.elsehere.world.repository;

import com.elsehere.world.entity.WorldObject;
import org.springframework.data.jpa.repository.JpaRepository;

public interface WorldObjectRepository extends JpaRepository<WorldObject, Long> {}
