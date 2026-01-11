# Future Improvements - Consolidated TODO

> **Last Updated:** January 12, 2026  
> This document consolidates all unimplemented recommendations from the various markdown files.
> The markdown files have been reviewed and all **completed** features are already in the codebase.
> 
> **Recent Additions:** Bible Reading Plans with 5 built-in plans, progress tracking, streak system

---

## ✅ IMPLEMENTED FEATURES (Reference)

The following major features are **already implemented** and working:

- ✅ Local AI with Phi-4 via Ollama
- ✅ RAG with Bible verses (WEB + KJV translations)
- ✅ 18 Biblical characters with unique personalities
- ✅ Modern MAUI UI with chat, prayers, reflections
- ✅ Multi-user system with PIN protection
- ✅ Character Memory System (characters remember users)
- ✅ Character Intelligence/Evolution System
- ✅ Cross-Character Learning (roundtable discussions)
- ✅ Cross-Device Sync via sync codes
- ✅ Response streaming
- ✅ Chat history with search
- ✅ Rating system (👍/👎) with JSONL export
- ✅ Theme toggle (Light/Dark/System)
- ✅ Font size preference setting
- ✅ Content moderation
- ✅ Offline AI models support (Ollama)
- ✅ Groq cloud fallback for mobile
- ✅ Azure OpenAI cloud integration
- ✅ Keyboard shortcuts infrastructure
- ✅ Daily devotionals backend
- ✅ Verse bookmarks backend
- ✅ Bible verse indexing service
- ✅ Secure API key storage (DPAPI)
- ✅ Performance optimizations (caching, connection pooling)
- ✅ Accessibility properties on key elements
- ✅ Serilog structured logging
- ✅ SQLite repositories for chat/prayer history
- ✅ GitHub Actions CI/CD pipeline
- ✅ Usage metrics service (local, anonymized)
- ✅ In-app feedback form
- ✅ Bible data compression/optimization
- ✅ Bible Reading Plans with 5 built-in plans

---

## 🔲 NOT YET IMPLEMENTED

### Priority 1: UI Pages Needed

#### 1. Devotionals Page ✅ IMPLEMENTED
**Backend:** ✅ Complete (`DevotionalRepository`, `Devotional` model)  
**UI:** ✅ Complete (`DevotionalPage.xaml`, `DevotionalViewModel.cs`)

**Features implemented:**
- Today's devotional view with Scripture, reflection, prayer
- AI-generated daily devotionals
- History list (last 7 days)
- Mark as read functionality
- Share devotional feature
- Keyboard shortcut: `Ctrl+D`

---

#### 2. Bookmarks Page ✅ IMPLEMENTED
**Backend:** ✅ Complete (`VerseBookmarkRepository`, `VerseBookmark` model)  
**UI:** ✅ Complete (`BookmarksPage.xaml`, `BookmarksViewModel.cs`)

**Features implemented:**
- List view of saved verses
- Category filtering (Comfort, Strength, Wisdom, etc.)
- Search by reference, text, notes
- Quick actions (edit note, share, delete)
- Add new bookmark dialog
- Keyboard shortcut: `Ctrl+B`

---

#### 3. Bible Reading Plan ✅ IMPLEMENTED
**Backend:** ✅ Complete (`ReadingPlanRepository`, `ReadingPlan` models)  
**UI:** ✅ Complete (`ReadingPlanPage.xaml`, `ReadingPlanViewModel.cs`)

**Features implemented:**
- 5 built-in reading plans (Bible in 90 Days, Psalms in 30 Days, Gospels in 60 Days, New Testament in 90 Days, Proverbs in 31 Days)
- Progress tracking with visual progress bar
- Streak tracking (current and longest streak)
- Mark day as complete/incomplete
- Navigation between days (Previous/Next)
- Day-by-day passages with titles and key verses
- Reflection prompts for each day
- Multiple plan types (Canonical, Chronological, Thematic, Gospel, Wisdom)
- Difficulty levels (Light, Medium, Intensive)
- Estimated reading time per day
- Completed plans history with dates
- Accessible via flyout menu "📅 Reading Plans"

---

#### 4. Biblical Background Information
**Backend:** ❌ Not implemented  
**UI:** ❌ Not implemented

**Features to implement:**
- Hebrew/Greek/Aramaic word studies
- Historical places database
- Archaeological discoveries
- Biblical people profiles
- Search functionality

---

### Priority 2: Feature Enhancements

#### 5. Dynamic Font Size Scaling ✅ IMPLEMENTED
**Status:** ✅ Complete

**Implementation:**
- Created `FontScaleService` with scale multipliers (Small=0.85, Medium=1.0, Large=1.15, XL=1.3)
- Added `ScaledSize*` dynamic resources in `App.xaml`
- Font scale applied on user settings change and app startup
- Service registered in DI container

---

#### 6. Keyboard Shortcuts Wiring ✅ IMPLEMENTED
**Status:** ✅ Complete

**Shortcuts implemented:**
- `Ctrl+N` → Character Selection (new chat)
- `Ctrl+H` → Chat History
- `Ctrl+P` → Prayer Journal
- `Ctrl+R` → Reflections
- `Ctrl+,` → Settings
- `Ctrl+W` → Wisdom Council
- `Ctrl+T` → Roundtable Discussion
- `Ctrl+Y` → Prayer Chain
- `Ctrl+Shift+D` → System Diagnostics

**Implementation:**
- Enhanced `KeyboardShortcutService` with navigation shortcuts
- Windows keyboard handling via `Platforms/Windows/App.xaml.cs`
- Shortcuts registered in `AppShell` on startup

---

#### 7. Full Bible Verse Index Population ✅ IMPLEMENTED
**Status:** ✅ Complete

**Implementation:**
- `BibleVerseIndexService` now populated with 31,098 verses on startup
- Background loading with parallel indexing for speed
- Word-based search index for fast verse lookup
- Verses loaded from `web.json` via MAUI FileSystem
- Index ready within seconds of app launch

---

#### 8. Enhanced Accessibility
**Status:** Basic properties added

**Remaining work:**
- Add `SemanticProperties.HeadingLevel` to section headers
- Implement proper keyboard navigation
- Add screen reader announcements for dynamic content
- Focus management improvements
- Test with Windows Narrator and JAWS

---

### Priority 3: Expand Content

#### 9. Full Bible Import ✅ ALREADY COMPLETE
**Status:** ✅ 31,098 verses already imported!

**📂 Source files:** `bible-playground/bible/` (1,498 HTML files)
**📄 Output:** `src/AI-Bible-App.Maui/Data/Bible/web.json` (342,080 lines)

**What's included:**
- All 66 canonical books
- Apocrypha/Deuterocanonical books (Tobit, Judith, Maccabees, etc.)
- Full verse text with references
- Book, chapter, verse structure
- Testament classification (Old/New)

**RAG Integration:** ✅ Already configured
- `WebBibleRepository.cs` loads all verses
- `BibleRAGService.cs` indexes for semantic search
- Embeddings generated via `nomic-embed-text` model

**Additional translations to add (future):**
| Translation | Status |
|-------------|--------|
| ASV (American Standard 1901) | 🔲 Add |
| Darby Translation | 🔲 Add |
| Young's Literal Translation | 🔲 Add |

**Commentaries/References:**
| Source | Status |
|--------|--------|
| Matthew Henry Commentary | 🔲 Add |
| Treasury of Scripture Knowledge | 🔲 Add |
| Strong's Concordance | 🔲 Add |
| Spurgeon's Sermons | 🔲 Add |

---

#### 10. Additional Characters (Future)
Current: 11 characters

**Phase 3 candidates:**
- Rahab – faith, transformation
- Sarah – promise, patience
- Miriam – worship, leadership
- Priscilla – teaching, partnership
- Lydia – business, hospitality
- Abraham – faith, covenant
- Joseph (OT) – dreams, forgiveness
- Daniel – faithfulness, visions

---

### Priority 4: Advanced Features

#### 11. Custom Mini-LLM Training
**Status:** Data collection in progress

**Phase 2 (Data Preparation):**
- [ ] Filter high-rated responses
- [ ] Create instruction-following pairs
- [ ] Add character voice examples
- [ ] Validate scripture accuracy

**Phase 3 (Fine-Tuning):**
- [ ] Choose base model (Phi-3, Llama 3.2, Qwen 2.5)
- [ ] Apply LoRA/QLoRA efficient fine-tuning
- [ ] Use DPO with ratings data
- [ ] Test character voice consistency

**Phase 4 (Deployment):**
- [ ] Convert to GGUF/ONNX
- [ ] Package custom model
- [ ] Ollama Modelfile distribution

---

#### 12. Export Enhancements
**Current:** Text-based share via SwipeView

**Future:**
- PDF export with formatting (QuestPDF)
- Include character avatars/images
- Email integration

---

#### 13. Embedding Cache Persistence
**Current:** In-memory only

**Enhancement:**
- Serialize RAG embeddings to disk
- Faster cold starts
- Persistent cache across sessions
- ~50MB disk space for 1000 queries

---

#### 14. Daily Notification System
**Status:** Not implemented

**Features:**
- Push notification for daily devotional
- User-configurable reminder time
- Background service for notifications

---

## 📋 Markdown Files Summary

| File | Status | Notes |
|------|--------|-------|
| `README.md` | ✅ Current | Main documentation |
| `DEVELOPER.md` | ✅ Current | Developer guide |
| `QUICK_START.md` | ✅ Current | Getting started |
| `LOCAL_AI_SETUP.md` | ✅ Complete | Ollama setup |
| `OLLAMA_SETUP.md` | ✅ Complete | Model configuration |
| `RAG_IMPLEMENTATION.md` | ✅ Complete | RAG architecture |
| `RAG_SUMMARY.md` | ✅ Complete | RAG overview |
| `RAG_VERIFICATION.md` | ✅ Complete | Testing RAG |
| `WEB_BIBLE_ADDITION.md` | ✅ Complete | Multi-translation |
| `MIGRATION_TO_PHI4.md` | ✅ Complete | Migration done |
| `MAUI_IMPLEMENTATION.md` | ✅ Complete | MAUI architecture |
| `MOBILE_DEPLOYMENT.md` | ✅ Current | iOS/Android guide |
| `PERFORMANCE_OPTIMIZATIONS.md` | ✅ Complete | All implemented |
| `IMPLEMENTATION_SUMMARY.md` | ⚠️ Outdated | Early project state |
| `CRITICAL_IMPLEMENTATION_COMPLETE.md` | ✅ Complete | Initial setup done |
| `E2E_TESTING_GUIDE.md` | ✅ Current | Testing scenarios |
| `FEATURES.md` | ⚠️ Partial | Reading Plan/Background not built |
| `IMPROVEMENTS_IMPLEMENTED.md` | ⚠️ Partial | Some UI pages missing |
| `GROK_SUGGESTIONS.md` | ⚠️ Reference | Roadmap document |
| `CONTRIBUTING.md` | ✅ Complete | Contributor guidelines |
| `SECURITY.md` | ✅ Complete | Privacy & security policy |
| `BRANCHING_STRATEGY.md` | ✅ Complete | Git workflow |
| `CHANGELOG.md` | ✅ Complete | Version history |

---

## 🎯 Recommended Next Steps

### Quick Wins (1-2 hours each)
1. ~~Create `DevotionalPage.xaml`~~ ✅ Done
2. ~~Create `BookmarksPage.xaml`~~ ✅ Done
3. ~~Wire keyboard shortcuts to ViewModels~~ ✅ Done

### Medium Effort (4-8 hours each)
4. ~~Implement dynamic font size scaling~~ ✅ Done
5. ~~Populate Bible verse index on startup~~ ✅ Done
6. ~~Add more Bible verses~~ ✅ Done (31,098 verses)

### Larger Features (1-2 days each)
7. Bible Reading Plan system
8. Biblical Background Information module
9. Daily notification system

### Infrastructure (Recently Completed)
10. ~~GitHub Actions CI/CD~~ ✅ Done
11. ~~Usage metrics service~~ ✅ Done
12. ~~Feedback form~~ ✅ Done
13. ~~Azure OpenAI integration~~ ✅ Done
14. ~~Serilog logging~~ ✅ Done
15. ~~SQLite persistence~~ ✅ Done

---

*This document replaces the need to reference multiple markdown files for TODO items.*
