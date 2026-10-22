package com.elsehere.vocab.repository;

import com.elsehere.vocab.entity.UserVocab;
import org.springframework.data.jpa.repository.JpaRepository;

public interface UserVocabRepository extends JpaRepository<UserVocab, Long> {}
