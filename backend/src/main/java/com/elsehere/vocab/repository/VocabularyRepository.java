package com.elsehere.vocab.repository;

import com.elsehere.vocab.entity.Vocabulary;
import org.springframework.data.jpa.repository.JpaRepository;

public interface VocabularyRepository extends JpaRepository<Vocabulary, Long> {}
