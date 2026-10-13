<script>
  import Icon from '../lib/Icon.svelte';

  const steps = [
    { icon: 'upload', title: 'Друг присылает сборку', text: 'Файл .modcrew — в нём список модов и инструкции, как их поставить.' },
    { icon: 'plus', title: 'Добавляешь её в ModCrew', text: '«Сборки» → «Добавить сборку». ModCrew сам найдёт игру в Steam.' },
    { icon: 'download', title: 'Жмёшь «Установить»', text: 'Всё скачается, распакуется и разложится по нужным папкам. Ничего делать руками не надо.' },
    { icon: 'undo', title: 'Передумал — «Удалить из игры»', text: 'Перед установкой ModCrew запоминает, что меняет, и может вернуть игру точно как было.' },
  ];

  const actions = [
    ['download', 'скачать файл по https (с проверкой sha256)'],
    ['extract', 'распаковать .zip'],
    ['copy', 'скопировать файл или папку'],
    ['delete', 'удалить файл или папку'],
    ['replaceText', 'заменить текст в файле'],
    ['setIni', 'поменять настройку в .ini'],
    ['run', 'запустить установщик (спросит игрока)'],
  ];
</script>

<div class="page">
  <h1>Как это работает</h1>
  <p class="sub">Для тех, кто не хочет разбираться в папках и модлоадерах</p>

  <div class="steps">
    {#each steps as s, i}
      <div class="step" style:animation-delay="{i * 60}ms">
        <div class="n">{i + 1}</div>
        <div class="ic"><Icon name={s.icon} size={22} /></div>
        <h3>{s.title}</h3>
        <p>{s.text}</p>
      </div>
    {/each}
  </div>

  <section class="safety">
    <div class="ic big"><Icon name="check" size={22} /></div>
    <div>
      <h3>Безопасно</h3>
      <p>
        Сборка — это не программа, а список простых действий. Она может менять файлы только в папке игры и в папках
        сохранений, но не в Windows, не на рабочем столе и не в других программах. Установщики модов запускаются
        только после твоего подтверждения.
      </p>
    </div>
  </section>

  <section class="author">
    <h2><Icon name="sparkle" /> Для автора сборки</h2>
    <p>
      «Сборки» → «Новая сборка» → выбери игру и добавляй моды: «Мод из файла» (zip или dll с диска — в том числе скачанные
      с Nexus) или «По ссылке». ModCrew заглянет в архив, подскажет, куда его положить, и сам составит шаги. Проверь
      установку кнопкой «Установить и проверить» и нажми «Сохранить .modcrew», чтобы отправить друзьям.
      Пути в шагах пишутся с переменными: <code>{'{game}'}</code> — папка игры, <code>{'{pack}'}</code> — папка сборки,
      <code>{'{work}'}</code> — временная папка, а также <code>{'{documents}'}</code>, <code>{'{appdata}'}</code>,
      <code>{'{localappdata}'}</code>, <code>{'{locallow}'}</code>.
    </p>
    <div class="actions">
      {#each actions as [name, text]}
        <div><code>{name}</code><span>{text}</span></div>
      {/each}
    </div>
  </section>
</div>

<style>
  .page { padding: 34px 40px 48px; max-width: 1100px; margin: 0 auto; }
  h1 { font-size: 30px; font-weight: 700; }
  .sub { color: var(--muted); margin: 4px 0 28px; }

  .steps { display: grid; grid-template-columns: repeat(4, 1fr); gap: 16px; }
  .step {
    position: relative; padding: 22px 20px; border-radius: var(--radius-lg);
    background: var(--panel); border: 1px solid var(--line); animation: fade-up 0.4s var(--ease) both;
  }
  .n { position: absolute; top: 16px; right: 18px; font-size: 36px; font-weight: 800; color: rgba(255, 255, 255, 0.05); }
  .ic {
    width: 44px; height: 44px; border-radius: 12px; display: grid; place-items: center; margin-bottom: 16px;
    background: var(--accent-soft); color: #a993ff;
  }
  .step h3 { font-size: 15px; margin-bottom: 6px; }
  .step p { color: var(--muted); font-size: 13px; margin: 0; line-height: 1.55; }

  .safety {
    display: flex; gap: 18px; margin-top: 18px; padding: 22px; border-radius: var(--radius-lg);
    background: linear-gradient(135deg, rgba(52, 211, 153, 0.08), rgba(34, 211, 238, 0.04));
    border: 1px solid rgba(52, 211, 153, 0.22);
  }
  .ic.big { background: rgba(52, 211, 153, 0.14); color: var(--ok); flex: none; margin: 0; }
  .safety h3 { font-size: 15px; margin-bottom: 4px; }
  .safety p { color: var(--muted); margin: 0; line-height: 1.6; }

  .author { margin-top: 36px; }
  .author h2 { font-size: 18px; display: flex; align-items: center; gap: 8px; margin-bottom: 10px; }
  .author h2 :global(svg) { color: #a993ff; }
  .author p { color: var(--muted); line-height: 1.7; max-width: 820px; user-select: text; }
  code { background: rgba(255, 255, 255, 0.07); padding: 1px 6px; border-radius: 6px; font-size: 12.5px; color: #d6ccff; user-select: text; }
  .actions { display: grid; grid-template-columns: repeat(2, 1fr); gap: 8px 24px; margin-top: 14px; }
  .actions div { display: flex; gap: 10px; align-items: baseline; color: var(--muted); font-size: 13px; }
  .actions code { min-width: 92px; text-align: center; }

  @media (max-width: 1100px) { .steps { grid-template-columns: repeat(2, 1fr); } }
</style>
