#!/usr/bin/env python3
"""
ThriftLoop Project Exporter
Usage: python export.py [--output file.txt] [--feature Feature1 Feature2] [--exclude .css .js] [--no-open]
"""

import os
import sys
import argparse
import subprocess
from pathlib import Path
from datetime import datetime

# Configuration
EXCLUDE_DIRS = {'bin', 'obj', '.git', '.vs', 'node_modules', 'Migrations', 'uploads', 'Properties', 'lib'}
INCLUDE_EXTS = {'.cs', '.cshtml', '.json', '.html', '.css', '.js'}
ALWAYS_INCLUDE = {'Program.cs', 'ApplicationDbContext.cs', 'BaseController.cs', 
                  '_Layout.cshtml', '_Navbar.cshtml', '_ViewImports.cshtml', '_ViewStart.cshtml'}

def estimate_tokens(text: str) -> int:
    """Estimate token count (rough approximation: 1 token ≈ 4 characters)."""
    return len(text) // 4

def clean_utf8_corruption(text: str) -> str:
    """Fix common UTF-8 corruption patterns."""
    replacements = {
        '\u00e2\u0094\u0080': '-',  # Box horizontal
        '\u00e2\u0094\u0082': '|',  # Box vertical
        '\u00e2\u0094\u009c': '|',  # Box T
        '\u00e2\u0080\u009c': '"',  # Smart quote left
        '\u00e2\u0080\u009d': '"',  # Smart quote right
        '\u00e2\u0080\u0099': "'",  # Apostrophe
        '\u00e2\u0080\u0093': '-',  # En dash
        '\u00c2': '',               # BOM fragment
    }
    for corrupt, clean in replacements.items():
        text = text.replace(corrupt, clean)
    return text

def should_exclude_file(file_path: Path, exclude_patterns: list) -> bool:
    """Check if file should be excluded based on patterns."""
    rel_path = str(file_path).replace('\\', '/')
    
    # Always exclude .cshtml.cs files (designer files)
    if file_path.suffix == '.cs' and file_path.stem.endswith('.cshtml'):
        return True
    
    # Always exclude wwwroot/lib contents
    if '/wwwroot/lib/' in rel_path or 'wwwroot\\lib\\' in rel_path:
        return True
    
    if not exclude_patterns:
        return False
    
    for pattern in exclude_patterns:
        # Check file extension
        if pattern.startswith('.'):
            if file_path.suffix.lower() == pattern.lower():
                return True
        # Check path contains keyword
        elif pattern.lower() in rel_path.lower():
            return True
    
    return False

def resolve_features(feature_names: list, exclude_patterns: list = None) -> set:
    """Find all files related to given features."""
    cwd = Path.cwd()
    feature_files = set()
    exclude_patterns = exclude_patterns or []
    
    # Always include these core files (unless excluded)
    for filename in ALWAYS_INCLUDE:
        for file in cwd.rglob(filename):
            if any(ex in file.parts for ex in EXCLUDE_DIRS):
                continue
            if not should_exclude_file(file, exclude_patterns):
                feature_files.add(file)
    
    # Find files matching feature names
    for feature in feature_names:
        # Look for Controllers, Views, Services, Repositories with this feature name
        patterns = [
            f"*{feature}*Controller.cs",
            f"*{feature}*.cshtml",
            f"*{feature}*Service.cs",
            f"*{feature}*Repository.cs",
            f"*{feature}*.cs"
        ]
        
        for pattern in patterns:
            for file in cwd.rglob(pattern):
                if any(ex in file.parts for ex in EXCLUDE_DIRS):
                    continue
                if should_exclude_file(file, exclude_patterns):
                    continue
                feature_files.add(file)
                
                # If it's a controller, also include its Views folder
                if 'Controller.cs' in file.name:
                    view_folder = cwd / 'Views' / file.stem.replace('Controller', '')
                    if view_folder.exists():
                        for view in view_folder.rglob('*.cshtml'):
                            if not should_exclude_file(view, exclude_patterns):
                                feature_files.add(view)
    
    return feature_files

def open_in_chrome(filepath: str):
    """Open the exported file in Chrome."""
    try:
        # Try to find Chrome in common locations
        chrome_paths = [
            r"C:\Program Files\Google\Chrome\Application\chrome.exe",
            r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
            os.path.expanduser(r"~\AppData\Local\Google\Chrome\Application\chrome.exe")
        ]
        
        chrome_exe = None
        for path in chrome_paths:
            if os.path.exists(path):
                chrome_exe = path
                break
        
        if chrome_exe:
            subprocess.Popen([chrome_exe, os.path.abspath(filepath)])
        else:
            # Fallback to default browser
            print("⚠️ Chrome not found, opening with default browser...")
            os.startfile(filepath)
            
    except Exception as e:
        print(f"⚠️ Could not open file automatically: {e}")
        print(f"📁 File location: {os.path.abspath(filepath)}")

def export_project(output_file: str, feature_filter: list = None, exclude_patterns: list = None, no_open: bool = False):
    """Export project files to a single text file."""
    cwd = Path.cwd()
    files_exported = 0
    total_chars = 0
    exclude_patterns = exclude_patterns or []
    excluded_count = 0
    
    # Collect files first to build content
    if feature_filter:
        all_files = sorted(resolve_features(feature_filter, exclude_patterns))
        print(f"🔍 Found {len(all_files)} files for feature(s): {', '.join(feature_filter)}")
    else:
        all_files = []
        for ext in INCLUDE_EXTS:
            for file in cwd.rglob(f'*{ext}'):
                if any(ex in file.parts for ex in EXCLUDE_DIRS):
                    continue
                if should_exclude_file(file, exclude_patterns):
                    excluded_count += 1
                    continue
                all_files.append(file)
        all_files.sort()
        print(f"📁 Found {len(all_files)} files (full export)")
    
    # Build content first to calculate token estimate
    content_buffer = []
    
    # Header
    content_buffer.append(f"Project: {cwd}\n")
    content_buffer.append(f"{cwd.name} Export\n")
    content_buffer.append(f"Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")
    if feature_filter:
        content_buffer.append(f"Features: {', '.join(feature_filter)}\n")
    content_buffer.append(f"Files: {len(all_files)}\n")
    content_buffer.append("Auto-excluded: .cshtml.cs files, wwwroot/lib/*\n")
    if exclude_patterns:
        content_buffer.append(f"User excluded: {', '.join(exclude_patterns)}\n")
    content_buffer.append("=" * 60 + "\n")
    
    # Directory tree
    content_buffer.append("\n📂 DIRECTORY STRUCTURE\n")
    content_buffer.append("=" * 60 + "\n")
    
    tree = {}
    for file in all_files:
        parts = file.relative_to(cwd).parts
        current = tree
        for part in parts[:-1]:
            if part not in current:
                current[part] = {}
            current = current[part]
        if '_files' not in current:
            current['_files'] = []
        current['_files'].append(parts[-1])
    
    def build_tree(node, indent=""):
        lines = []
        items = sorted([k for k in node.keys() if k != '_files'])
        for i, key in enumerate(items):
            is_last = i == len(items) - 1
            prefix = "└── " if is_last else "├── "
            lines.append(f"{indent}{prefix}{key}/\n")
            next_indent = indent + ("    " if is_last else "│   ")
            lines.extend(build_tree(node[key], next_indent))
        
        if '_files' in node:
            files = sorted(node['_files'])
            for i, f in enumerate(files):
                is_last = i == len(files) - 1
                prefix = "└── " if is_last else "├── "
                lines.append(f"{indent}{prefix}{f}\n")
        return lines
    
    content_buffer.extend(build_tree(tree))
    
    # File contents header
    content_buffer.append("\n" + "=" * 60 + "\n")
    content_buffer.append("📄 FILE CONTENTS\n")
    content_buffer.append("=" * 60 + "\n")
    
    # Process files and collect content
    file_contents = []
    for file in all_files:
        try:
            content = file.read_text(encoding='utf-8')
            content = clean_utf8_corruption(content)
            
            file_section = []
            file_section.append(f"\n--- {file.relative_to(cwd)} ---\n")
            file_section.append(content)
            if not content.endswith('\n'):
                file_section.append('\n')
            
            file_contents.append(''.join(file_section))
            total_chars += sum(len(s) for s in file_section)
            files_exported += 1
            
        except Exception as e:
            error_msg = f"\n--- ERROR reading {file}: {e} ---\n"
            file_contents.append(error_msg)
            total_chars += len(error_msg)
    
    # Calculate token estimate
    total_chars += sum(len(s) for s in content_buffer)
    token_estimate = estimate_tokens(''.join(content_buffer + file_contents))
    
    # Insert token estimate at the top
    token_info = [
        f"{'=' * 60}\n",
        f"📊 ESTIMATED TOKEN CONSUMPTION\n",
        f"{'=' * 60}\n",
        f"Characters: {total_chars:,}\n",
        f"Estimated Tokens: ~{token_estimate:,} (1 token ≈ 4 chars)\n",
        f"Files: {files_exported}\n",
        f"{'=' * 60}\n\n"
    ]
    
    # Write everything to file
    with open(output_file, 'w', encoding='utf-8-sig') as out:
        out.write(''.join(token_info))
        out.write(''.join(content_buffer))
        out.write(''.join(file_contents))
    
    print(f"📊 Estimated tokens: ~{token_estimate:,}")
    print(f"✅ Exported {files_exported} files to {output_file}")
    
    if exclude_patterns:
        print(f"🚫 User excluded patterns: {', '.join(exclude_patterns)}")
    print(f"🚫 Auto-excluded: .cshtml.cs files, wwwroot/lib/*")
    
    # Open in Chrome (unless --no-open was specified)
    if not no_open:
        print("🌐 Opening in Chrome...")
        open_in_chrome(output_file)

def list_features():
    """List all detected features from Controllers."""
    cwd = Path.cwd()
    features = set()
    
    for file in cwd.rglob('*Controller.cs'):
        if any(ex in file.parts for ex in EXCLUDE_DIRS):
            continue
        if file.stem != 'BaseController':
            features.add(file.stem.replace('Controller', ''))
    
    print("\n  Detected Features (from Controllers)")
    print("  " + "-" * 50)
    for f in sorted(features):
        print(f"    -Feature {f}")
    print()

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Export ThriftLoop project files')
    parser.add_argument('--output', '-o', default='Project_Export.txt', help='Output file name')
    parser.add_argument('--feature', '-f', nargs='+', help='Filter by feature name(s)')
    parser.add_argument('--exclude', '-e', nargs='+', help='Exclude files by extension (.css) or path keyword')
    parser.add_argument('--list-features', '-l', action='store_true', help='List all detected features')
    parser.add_argument('--no-open', '-n', action='store_true', help='Skip opening the output file in Chrome')
    
    args = parser.parse_args()
    
    if args.list_features:
        list_features()
    else:
        export_project(args.output, args.feature, args.exclude, args.no_open)