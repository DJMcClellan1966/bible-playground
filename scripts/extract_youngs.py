"""Extract Young's Literal Translation verses from HTML files and create JSON for the app."""
import os
import json
import re
from pathlib import Path

def extract_verses_from_html(html_content):
    """Extract verses using regex from HTML content."""
    verses = []
    
    # First, remove footnote popups
    html_content = re.sub(r'<span class="popup">.*?</span>', '', html_content, flags=re.DOTALL)
    
    # Find all verse markers and extract text between them
    pattern = r'id="V(\d+)">(\d+)&#160;</span>(.*?)(?=<span class="verse"|</div>)'
    
    matches = re.findall(pattern, html_content, re.DOTALL)
    
    for verse_num_str, display_num, text in matches:
        verse_num = int(verse_num_str)
        
        # Clean up the text
        # Remove HTML tags
        text = re.sub(r'<[^>]+>', ' ', text)
        # Decode HTML entities
        text = text.replace('&#160;', ' ')
        text = text.replace('&nbsp;', ' ')
        text = text.replace('&lt;', '<')
        text = text.replace('&gt;', '>')
        text = text.replace('&amp;', '&')
        text = text.replace('&quot;', '"')
        text = text.replace('&apos;', "'")
        text = text.replace('[', '')
        text = text.replace(']', '')
        # Remove footnote markers
        text = re.sub(r'[†‡§¶]', '', text)
        # Clean up whitespace
        text = re.sub(r'\s+', ' ', text).strip()
        
        if text:
            verses.append({
                "verse_num": verse_num,
                "text": text
            })
    
    return verses

# Book name mapping
BOOK_NAMES = {
    "GEN": "Genesis", "EXO": "Exodus", "LEV": "Leviticus", "NUM": "Numbers", "DEU": "Deuteronomy",
    "JOS": "Joshua", "JDG": "Judges", "RUT": "Ruth", "1SA": "1 Samuel", "2SA": "2 Samuel",
    "1KI": "1 Kings", "2KI": "2 Kings", "1CH": "1 Chronicles", "2CH": "2 Chronicles",
    "EZR": "Ezra", "NEH": "Nehemiah", "EST": "Esther", "JOB": "Job",
    "PSA": "Psalms", "PRO": "Proverbs", "ECC": "Ecclesiastes", "SNG": "Song of Solomon",
    "ISA": "Isaiah", "JER": "Jeremiah", "LAM": "Lamentations", "EZK": "Ezekiel", "DAN": "Daniel",
    "HOS": "Hosea", "JOL": "Joel", "AMO": "Amos", "OBA": "Obadiah", "JON": "Jonah",
    "MIC": "Micah", "NAM": "Nahum", "HAB": "Habakkuk", "ZEP": "Zephaniah", "HAG": "Haggai",
    "ZEC": "Zechariah", "MAL": "Malachi",
    "MAT": "Matthew", "MRK": "Mark", "LUK": "Luke", "JHN": "John", "ACT": "Acts",
    "ROM": "Romans", "1CO": "1 Corinthians", "2CO": "2 Corinthians", "GAL": "Galatians",
    "EPH": "Ephesians", "PHP": "Philippians", "COL": "Colossians", "1TH": "1 Thessalonians",
    "2TH": "2 Thessalonians", "1TI": "1 Timothy", "2TI": "2 Timothy", "TIT": "Titus",
    "PHM": "Philemon", "HEB": "Hebrews", "JAS": "James", "1PE": "1 Peter", "2PE": "2 Peter",
    "1JN": "1 John", "2JN": "2 John", "3JN": "3 John", "JUD": "Jude", "REV": "Revelation"
}

OLD_TESTAMENT_BOOKS = [
    "Genesis", "Exodus", "Leviticus", "Numbers", "Deuteronomy", "Joshua", "Judges", "Ruth",
    "1 Samuel", "2 Samuel", "1 Kings", "2 Kings", "1 Chronicles", "2 Chronicles",
    "Ezra", "Nehemiah", "Esther", "Job", "Psalms", "Proverbs", "Ecclesiastes",
    "Song of Solomon", "Isaiah", "Jeremiah", "Lamentations", "Ezekiel", "Daniel",
    "Hosea", "Joel", "Amos", "Obadiah", "Jonah", "Micah", "Nahum", "Habakkuk",
    "Zephaniah", "Haggai", "Zechariah", "Malachi"
]

def extract_verses_from_file(filepath):
    """Extract all verses from an HTML file."""
    with open(filepath, 'r', encoding='utf-8') as f:
        html_content = f.read()
    
    return extract_verses_from_html(html_content)

def parse_filename(filename):
    """Parse book code and chapter from filename like 'JHN03.htm'."""
    # Remove extension
    name_no_ext = filename.replace('.htm', '')
    # Match book code (non-greedy) followed by chapter digits
    match = re.match(r'([A-Z0-9]+?)(\d+)$', name_no_ext)
    if match:
        book_code = match.group(1)
        chapter = int(match.group(2))
        return book_code, chapter
    return None, None

def main():
    bible_dir = Path(r"C:\Users\DJMcC\OneDrive\Desktop\bible-playground\bible\youngs")
    output_file = Path(r"C:\Users\DJMcC\OneDrive\Desktop\bible-playground\src\AI-Bible-App.Maui\Data\Bible\youngs.json")
    
    all_verses = []
    
    # Get all HTML chapter files
    html_files = sorted([f for f in os.listdir(bible_dir) if re.match(r'[A-Z0-9]+\d+\.htm', f)])
    
    print(f"Found {len(html_files)} chapter files in Young's Literal Translation")
    
    # Test first file
    if html_files:
        test_file = html_files[0]
        print(f"Testing first file: {test_file}")
        test_path = bible_dir / test_file
        test_verses = extract_verses_from_file(test_path)
        print(f"Test extraction resulted in {len(test_verses)} verses")
        if test_verses:
            print(f"Sample verse: {test_verses[0]['text'][:100]}...")
    
    for filename in html_files:
        book_code, chapter = parse_filename(filename)
        if not book_code:
            continue
        if book_code not in BOOK_NAMES:
            # print(f"Skipping {filename} - unknown book code: {book_code}")
            continue
        
        book_name = BOOK_NAMES[book_code]
        testament = "Old" if book_name in OLD_TESTAMENT_BOOKS else "New"
        
        filepath = bible_dir / filename
        verses = extract_verses_from_file(filepath)
        
        if not verses:
            print(f"WARNING: No verses found in {filename}")
            continue
        
        for verse_data in verses:
            verse_num = verse_data["verse_num"]
            text = verse_data["text"]
            
            verse_obj = {
                "Book": book_name,
                "Chapter": chapter,
                "Verse": verse_num,
                "Text": text,
                "Translation": "YLT",
                "Reference": f"{book_name} {chapter}:{verse_num}",
                "FullText": f"{book_name} {chapter}:{verse_num}: {text}",
                "Testament": testament,
                "BookNumber": 0
            }
            all_verses.append(verse_obj)
        
        if len(all_verses) % 500 == 0:
            print(f"Processed {len(all_verses)} verses so far...")
    
    # Create output directory if needed
    output_file.parent.mkdir(parents=True, exist_ok=True)
    
    # Write JSON file
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(all_verses, f, indent=2, ensure_ascii=False)
    
    print(f"\nTotal verses extracted: {len(all_verses)}")
    print(f"Output written to: {output_file}")
    print(f"\nYoung's Literal Translation has been successfully extracted!")

if __name__ == "__main__":
    main()
