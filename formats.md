# 两个输入源的文件格式

KindleMate2 的数据全部来自 Kindle 设备上的两个文件：

| 文件 | 设备路径 | 内容 |
|---|---|---|
| `My Clippings.txt` | `documents/My Clippings.txt` | 标注 / 笔记 / 书签 |
| `vocab.db` | `system/vocabulary/vocab.db` | 生词本 + 查词记录（SQLite） |

本文档记录它们的**结构与字段语义**，供解析代码的维护者参考。

> **本文档不含任何真实数据** —— 所有样本都是合成值，不含真实书名、词条或设备标识。
> 结构与语义来自对真机文件的实测（2026-10-03）。凡标注"实测"的分布性结论，
> 都来自**一次**设备样本，**不能当作普遍规律**。

---

## 一、`My Clippings.txt`

### 1.1 整体

- 纯文本，UTF-8（可能带 BOM）
- 行尾：**未实测**（本项目未在真机样本上核实）。程序写回时统一用 CRLF —— 理由是跨平台产物一致，且对齐只在 Windows 上运行的原版行为。
- 条目之间以一行 `==========` 分隔
- **追加式**：Kindle 只往里追加，写满后滚动覆盖最旧的。因此同一内容可能重复出现

### 1.2 单条结构

标准 4 行：

```
Some Book Title (Some Author)
- 您在第 12 页（位置 #345-347）的标注 | 添加于 2016年6月16日星期四 下午1:47:27

标注正文……
==========
```

| 行 | 内容 |
|---|---|
| 1 | 书名与作者 |
| 2 | 类型 + 位置/页码 + 添加时间 |
| 3 | 空行 |
| 4+ | 正文（可多行，但实测极罕见） |
| 末 | `==========` |

### 1.3 第 1 行：书名与作者

作者可能被 `(...)` / `（...）` 包裹，也可能用 ` - ` 分隔，也可能整个缺失。
解析见 `MyClippingsHelper.ParseTitleAndAuthor`。

**这一行没有书的稳定标识**，只有字符串 —— 见 §四。

### 1.4 第 2 行：多语言

随设备语言变化。中文与英文的实际形态：

```
- 您在第 12 页（位置 #345-347）的标注 | 添加于 2016年6月16日星期四 下午1:47:27
- 您在第 8 页（位置 #575）的笔记 | 添加于 2016年6月16日星期四 下午4:31:44

- Your Highlight on page 12 | Location 345-347 | Added on Friday, January 17, 2020 9:29:28 PM
```

- **类型词**：标注 / 笔记 / 书签 / 文章剪切（英：Highlight / Note / Bookmark / Cut）。
  完整多语言词表见 `BriefTypeTranslations`（10 语言覆盖）
- **位置**：`位置 #起-止`；单点时只有起。英：`Location a-b`
- **页码**：`第 N 页`。英：`page N`
- **时间**：设备**本地时间**的自然语言文本，**不含时区**。
  解析见 `MyClippingsHelper.TryParseClippingDate`
  （清洗前缀/星期词 → 三种精确格式 → 11 个 Kindle 文化轮询 → 数字日期兜底）

### 1.5 实测分布

在一次真机样本（数千条）中：

- **标注占绝大多数**，笔记很少
- **书签 0 条** —— 这是该设备的样本。书签条目没有正文，若出现，正文行为空
- **多行正文极罕见**：数千条里只有个位数超过 4 行
- 时间跨度可达数年
- 元数据行绝大多数为中文（由设备语言决定）

---

## 二、`vocab.db`

### 2.1 schema

照抄 `sqlite_master`（表名**全大写**）：

```sql
CREATE TABLE BOOK_INFO (id TEXT PRIMARY KEY NOT NULL, asin TEXT, guid TEXT,
                        lang TEXT, title TEXT, authors TEXT);

CREATE TABLE DICT_INFO (id TEXT PRIMARY KEY NOT NULL, asin TEXT,
                        langin TEXT, langout TEXT);

CREATE TABLE LOOKUPS   (id TEXT PRIMARY KEY NOT NULL, word_key TEXT, book_key TEXT,
                        dict_key TEXT, pos TEXT, usage TEXT, timestamp INTEGER DEFAULT 0);

CREATE TABLE METADATA  (id TEXT PRIMARY KEY NOT NULL, dsname TEXT,
                        sscnt INTEGER, profileid TEXT);

CREATE TABLE VERSION   (id TEXT PRIMARY KEY NOT NULL, dsname TEXT, value INTEGER);

CREATE TABLE WORDS     (id TEXT PRIMARY KEY NOT NULL, word TEXT, stem TEXT, lang TEXT,
                        category INTEGER DEFAULT 0, timestamp INTEGER DEFAULT 0, profileid TEXT);
```

设备**自建**的索引（说明它预期按这些列查询）：

```sql
CREATE INDEX lookupbookkey ON LOOKUPS (book_key);
CREATE INDEX lookupwordkey ON LOOKUPS (word_key);
CREATE INDEX wordprofileid ON WORDS (profileid);
```

### 2.2 字段语义

#### `WORDS`

| 列 | 语义 |
|---|---|
| `id` | **`lang + ':' + word`**，如 `en:apple`。冗余，可由后两列派生 |
| `word` | 词本身 |
| `stem` | 词干。中文没有词干，实测取值规则不统一（有时取首字，有时取全词） |
| `lang` | 语言码（`en` / `zh` …） |
| `category` | 默认 0。**实测样本中全为 0，未被使用** |
| `timestamp` | **unix 毫秒** |
| `profileid` | 实测为**空字符串**，未被使用 |

#### `LOOKUPS`

| 列 | 语义 |
|---|---|
| `id` | **`<book_key>:<pos>:<seq>`** |
| `word_key` | 指向 `WORDS.id` |
| `book_key` | 指向 `BOOK_INFO.id` |
| `dict_key` | 指向 `DICT_INFO.id`。实测**多数为空字符串** |
| `pos` | ⚠️ **书中位置**，不是词性。与 `id` 的第三段相同 |
| `usage` | **查词时所在的上下文句子**，不是词典释义 |
| `timestamp` | unix 毫秒 |

> `LOOKUPS` **没有** `profileid` 列。

#### `BOOK_INFO`

| 列 | 语义 |
|---|---|
| `id` | `<书名转写>:<8 位十六进制>`，如 `Some_Book_Title:1A2B3C4D` |
| `asin` | 亚马逊 ASIN（如 `B012345678`），或 sideload 书的 UUID |
| `guid` | 实测**与 `id` 完全相同**（冗余列） |
| `lang` | 书语言，实测可能为空 |
| `title` / `authors` | 书名 / 作者 |

#### `DICT_INFO`

`id` / `asin` / `langin` / `langout`。实测行数很少，且 **`id` 可能是空字符串**。

#### `METADATA` / `VERSION`

设备自己的**表版本追踪**，`id` 就是表名（`WORDS` / `LOOKUPS` / …）。
与业务数据无关，不需要导入。

### 2.3 外键关系

三条关系在实测数据上**全部成立**，但 schema 里**没有 FK 约束**：

```
LOOKUPS.word_key  →  WORDS.id
LOOKUPS.book_key  →  BOOK_INFO.id
LOOKUPS.dict_key  →  DICT_INFO.id
```

---

## 三、已知坑

1. **`pos` 不是词性。** 名字极具误导性，它是**书中位置**（纯数字）。
   vocab.db 里**没有词性信息**。
2. **`LOOKUPS` 没有 `profileid`。** 只有 `WORDS` 和 `METADATA` 有这一列。
3. **`WORDS.id = lang + ':' + word`** —— 不要把它当成独立的业务标识。
4. **`BOOK_INFO.id == guid`** —— `guid` 是冗余的。
5. **`category` / `profileid` 实测未被使用**（全 0 / 全空串），不要指望源里有值。
6. **`timestamp` 的默认值是 `0` 而不是 `NULL`** —— 处理"缺失时间"时要区分
   "显式写入的 NULL" 与 "schema 默认的 0"。
7. **表名全大写**：`WORDS` / `LOOKUPS` / `BOOK_INFO` / `DICT_INFO` / `METADATA` / `VERSION`。
8. **空字符串也算"外键成立"**：`DICT_INFO.id` 与 `LOOKUPS.dict_key` 都可能是空字符串。

---

## 四、两个源的不对称

| | `My Clippings.txt` | `vocab.db` |
|---|---|---|
| 主键 | **无** | 每表都有 TEXT 主键 |
| 书的标识 | 只有 `书名 (作者)` 字符串 | `book_key` + `asin` + `guid` |
| 时间 | 本地时间文本，**无时区** | unix 毫秒（UTC） |
| 位置 | 页码 + 位置区间 | 单点位置（`pos`） |
| 删除记录 | 无 | 无 |

**两边没有共同的书标识**，只能靠书名 + 作者做启发式对齐。设备上的 `.sdr` 目录名提供了
一条**部分可用**的桥接（见 5.3），但它依赖目录名里带 ASIN 后缀、且 `My Clippings` 那一侧
仍需模糊匹配 —— 这是一条**不完整的**桥，不等于"硬约束已被解除"。

---

## 五、设备上的其他数据源（存在，但本项目未使用）

实测（2026-10-03，一台 Paperwhite Signature Edition）确认了以下库/目录的存在与用途，
**本项目均未读取**。记在这里是为了避免重复调研。

一台设备上共有 **21 个 `.db` 文件**（`mtp-filetree` 实测）。

### 5.1 `system/ksdk/.annotations/<账号ID>/ksdk_annotation_v1.db` —— **是空的**

Amazon 的标注同步框架，9 张表：`book_state` / `delta_sync_tokens` / `key_value_storage` /
`legacy_delta_sync_tokens` / `local_edit` / `migration_states` / `nonsyncable_annotations` /
`server_view` / `staging_server_view`。

**实测该设备上所有业务表都是 0 行**，只有 `key_value_storage` 有一行
（`ANNOTATION_RECOVERY_STATUS = SUCCESS`）。

→ **不能当作 `My Clippings.txt` 之外的第二个标注源。**

### 5.2 `documents/<书名>.sdr/*.cache.db` —— 阅读速度缓存

表 `per_book_speed_cache(profile_digest, average_wpm, total_words_read, total_time_read,
sample_count, running_sum, …)`。

**文件名里的那串哈希是用户 profile digest，不是书标识。**

→ 与"书"无关，**不能用来做书的对齐**。

### 5.3 `documents/<书名>.sdr/` 的目录名 —— **可以桥接到 `BOOK_INFO.asin`** ⭐

本次调研最有价值的发现。

部分 `.sdr` 目录名带 `_<ASIN 或 UUID>` 后缀，**该后缀就是 `vocab.db` 的 `BOOK_INFO.asin`**：

```
documents/Some_Book_Title_1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d.sdr
                          ↓ 提取后缀
vocab.db  BOOK_INFO.asin = 1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d
          BOOK_INFO.id   = Some_Book_Title:0A1B2C3D   ← 即 book_key
```

**覆盖率**（实测）：56 个 `.sdr` 里 13 个带可识别后缀（**13/56 ≈ 23%**）；
在 `BOOK_INFO` 那 10 行里覆盖了 **9 行**。

> ⚠️ **注意分母**：`BOOK_INFO` **只收录有查词记录的书**（本机仅 10 行），不是"设备上所有书"。
> 所以「9/10」**不能**读成"90% 的书都能桥接" —— 按 `.sdr` 目录算是 **13/56 ≈ 23%**。
> 两个数字都对，但说的不是一回事。

**后缀的两种形态**：

| 形态 | 正则 | 说明 |
|---|---|---|
| UUID | `[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}` | sideload 的书 |
| ASIN | `[A-Z0-9]{10}` | ⚠️ **不限于 `B0` 前缀** —— 实测存在 `B1` 开头的形态 |

**两个限制**：

1. **只有带后缀的 `.sdr` 能用**（实测 13/56）。无后缀的多是 sideload 的书。
2. **`My Clippings.txt` 的书名 ↔ `.sdr` 目录名这一段只能靠字符串匹配，而目录名有长度截断** ——
   实测一个长书名在目录名里被截断，**精确匹配会失败，必须前缀/模糊匹配**。

**结论**：`.sdr` ↔ `vocab.db` 这一段是**精确**的（靠 ASIN）；`My Clippings` ↔ `.sdr`
那一段是**模糊**的。两个源之间因此存在一条**部分可用**的对齐路径。

### 5.4 `.sdr` 目录里的其他文件（未深入）

实测扩展名分布：`.apnx`(33) / `.azw3r`(18) / `.azw3f`(18) / `.db`(12) / `.bad_file`(9) /
`.mbp1`(5) / `.mbs`(2) / `.lua`(2) / `.png`(1)。

其中 `.apnx` 是页码索引、`.mbp1` 是旧式标注文件 —— **均未调研**。

### 5.5 其他未调研的库

`system/readingstreams/readingstreams.db`、`system/freetime/freetime.db`、
`system/fmcache/fmcache.db`、`kmc/kpm/kpm.db`、`audible/default.hushpuppy.db` ——
存在，用途未知。
