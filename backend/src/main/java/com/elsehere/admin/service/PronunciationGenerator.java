package com.elsehere.admin.service;

import com.elsehere.admin.dto.PronunciationProgress;
import com.elsehere.admin.dto.PronunciationRun;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.ai.service.AiGateway;
import com.elsehere.vocab.entity.Vocabulary;
import com.elsehere.vocab.repository.VocabularyRepository;
import com.elsehere.world.service.PronunciationAudioStore;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;
import org.springframework.transaction.support.TransactionTemplate;

/**
 * 사물 단어 발음(WORLD-04)을 AI 워커 TTS로 만들어 발음 폴더에 저장한다. 관리자가 발음 생성 API로 실행한다(docs/admin.md).
 *
 * <p>발음 파일이 이미 있는 단어는 다시 만들지 않으므로 여러 번 실행해도 된다. 합성은 단어 하나에 몇 초씩 걸리므로 DB 트랜잭션 밖에서 하고, 발음 키만 짧은
 * 트랜잭션으로 넣는다.
 */
@Slf4j
@Service
public class PronunciationGenerator {

  private static final double SPEED = 1.0;

  private final VocabularyRepository vocabularyRepository;
  private final PronunciationAudioStore pronunciationAudioStore;
  private final AiGateway aiGateway;
  private final TransactionTemplate transactionTemplate;
  private final String voice;

  public PronunciationGenerator(
      VocabularyRepository vocabularyRepository,
      PronunciationAudioStore pronunciationAudioStore,
      AiGateway aiGateway,
      TransactionTemplate transactionTemplate,
      @Value("${elsehere.pronunciation.voice}") String voice) {
    this.vocabularyRepository = vocabularyRepository;
    this.pronunciationAudioStore = pronunciationAudioStore;
    this.aiGateway = aiGateway;
    this.transactionTemplate = transactionTemplate;
    this.voice = voice;
  }

  public PronunciationProgress progress() {
    List<ObjectWord> words = objectWords();
    int ready =
        (int)
            words.stream().filter(word -> pronunciationAudioStore.exists(word.audioKey())).count();
    return new PronunciationProgress(words.size(), ready);
  }

  /** 발음 파일이 없는 사물 단어의 발음을 만든다. 실패한 단어는 건너뛰고 나머지를 계속 만든다. */
  public PronunciationRun generateMissing(Long adminUserId) {
    int created = 0;
    List<String> failedWordKeys = new ArrayList<>();
    for (ObjectWord word : objectWords()) {
      if (!pronunciationAudioStore.exists(word.audioKey())) {
        try {
          synthesize(word, adminUserId);
          created++;
        } catch (RuntimeException exception) {
          // 워커 오류·시간 초과 등으로 한 단어가 실패해도 나머지 단어는 만든다. 다시 실행하면 실패한 단어만 다시 만든다.
          log.warn("pronunciation_word_failed word_key={}", word.wordKey(), exception);
          failedWordKeys.add(word.wordKey());
          continue;
        }
      }
      attach(word);
    }
    log.info(
        "pronunciation_generation_finished created={} failed={}", created, failedWordKeys.size());
    return new PronunciationRun(created, failedWordKeys);
  }

  private void synthesize(ObjectWord word, Long adminUserId) {
    WorkerTrace trace = new WorkerTrace(UUID.randomUUID(), adminUserId, null);
    byte[] wav = aiGateway.synthesize(trace, word.text(), voice, SPEED);
    pronunciationAudioStore.save(word.audioKey(), wav);
  }

  private void attach(ObjectWord word) {
    transactionTemplate.executeWithoutResult(
        status ->
            vocabularyRepository
                .findById(word.vocabId())
                .ifPresent(vocabulary -> vocabulary.attachPronunciation(word.audioKey())));
  }

  private List<ObjectWord> objectWords() {
    return transactionTemplate.execute(
        status ->
            vocabularyRepository.findAllByWordKeyIsNotNullOrderByIdAsc().stream()
                .map(ObjectWord::of)
                .toList());
  }

  /**
   * @param audioKey Unity 에셋에 적은 발음 키가 있으면 그 값, 없으면 {@code {wordKey}.wav}
   */
  private record ObjectWord(Long vocabId, String wordKey, String text, String audioKey) {

    static ObjectWord of(Vocabulary vocabulary) {
      String audioKey =
          vocabulary.getPronunciationAudioKey() != null
              ? vocabulary.getPronunciationAudioKey()
              : PronunciationAudioStore.keyOf(vocabulary.getWordKey());
      return new ObjectWord(
          vocabulary.getId(), vocabulary.getWordKey(), vocabulary.getText(), audioKey);
    }
  }
}
