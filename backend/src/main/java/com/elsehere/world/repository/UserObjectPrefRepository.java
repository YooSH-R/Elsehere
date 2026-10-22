package com.elsehere.world.repository;

import com.elsehere.world.entity.UserObjectPref;
import com.elsehere.world.entity.UserObjectPrefId;
import org.springframework.data.jpa.repository.JpaRepository;

public interface UserObjectPrefRepository extends JpaRepository<UserObjectPref, UserObjectPrefId> {}
