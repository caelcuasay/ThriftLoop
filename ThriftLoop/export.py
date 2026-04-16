#!/usr/bin/env python3
"""
ThriftLoop Project Exporter
Usage: python export.py [--output file.txt] [--feature Feature1 Feature2] [--folder folder1 folder2] [--exclude .css .js] [--no-open]
"""

import os
import sys
import argparse
import subprocess
import re
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
    if file_path.name.endswith('.cshtml.cs'):
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

def normalize_feature_name(feature: str) -> str:
    """Normalize feature name by removing 's' suffix if present."""
    # Remove trailing 's' if it exists, but keep singular form
    if feature.endswith('s'):
        singular = feature[:-1]
        return singular
    return feature

def find_controller_variants(feature: str) -> list:
    """Find all possible controller name variants for a feature."""
    singular = normalize_feature_name(feature)
    plural = singular + 's'
    
    variants = [
        singular,           # Item
        plural,            # Items
        singular.capitalize(),  # Item (capitalized)
        plural.capitalize(),    # Items (capitalized)
    ]
    
    patterns = []
    for variant in variants:
        patterns.extend([
            f"*{variant}*Controller.cs",
            f"*{variant}Controller.cs",
            f"*{variant}sController.cs",
        ])
    
    return list(set(patterns))  # Remove duplicates

def resolve_features(feature_names: list, exclude_patterns: list = None) -> set:
    """Find all files related to given features by tracing dependencies."""
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
    
    for feature in feature_names:
        print(f"  🔍 Tracing feature: {feature}")
        
        normalized_feature = normalize_feature_name(feature)
        singular = normalized_feature
        plural = singular + 's'
        
        # STEP 1: Find the controller(s) for this feature using multiple variants
        controller_patterns = find_controller_variants(feature)
        
        controllers_found = []
        for pattern in controller_patterns:
            for controller_file in cwd.rglob(pattern):
                if any(ex in controller_file.parts for ex in EXCLUDE_DIRS):
                    continue
                if should_exclude_file(controller_file, exclude_patterns):
                    continue
                if controller_file not in controllers_found:
                    controllers_found.append(controller_file)
                    feature_files.add(controller_file)
                    print(f"     Found controller: {controller_file.relative_to(cwd)}")
        
        # STEP 2: For each controller found, find its corresponding Views folder
        for controller in controllers_found:
            controller_name = controller.stem.replace('Controller', '')
            
            # Try multiple view folder naming conventions
            view_folders = [
                cwd / 'Views' / controller_name,
                cwd / 'Views' / f"{controller_name}s",
                cwd / 'Views' / singular.capitalize(),
                cwd / 'Views' / plural.capitalize(),
                cwd / 'Views' / singular,
                cwd / 'Views' / plural,
            ]
            
            for view_folder in view_folders:
                if view_folder.exists():
                    print(f"     Found views folder: {view_folder.relative_to(cwd)}")
                    for view in view_folder.rglob('*.cshtml'):
                        if not should_exclude_file(view, exclude_patterns):
                            feature_files.add(view)
            
            # Check Areas
            areas_folder = cwd / 'Areas'
            if areas_folder.exists():
                for area in areas_folder.iterdir():
                    if area.is_dir():
                        for view_name in [controller_name, f"{controller_name}s", singular, plural]:
                            area_views = area / 'Views' / view_name
                            if area_views.exists():
                                for view in area_views.rglob('*.cshtml'):
                                    if not should_exclude_file(view, exclude_patterns):
                                        feature_files.add(view)
        
        # STEP 3: Find services and repositories by parsing controller
        for controller in controllers_found:
            try:
                content = controller.read_text(encoding='utf-8')
                
                # Find injected interfaces in constructor
                injection_pattern = r'(?:public|private|protected)\s+\w+Controller\s*\([^)]*\)'
                constructor_match = re.search(injection_pattern, content)
                
                if constructor_match:
                    constructor_text = constructor_match.group(0)
                    interfaces = re.findall(r'I[A-Z][a-zA-Z0-9]+', constructor_text)
                    
                    for interface_name in interfaces:
                        impl_name = interface_name[1:]  # Remove 'I'
                        
                        # Search for implementation files
                        for file in cwd.rglob(f"*{impl_name}.cs"):
                            if any(ex in file.parts for ex in EXCLUDE_DIRS):
                                continue
                            if should_exclude_file(file, exclude_patterns):
                                continue
                            feature_files.add(file)
                            print(f"     Found dependency: {file.relative_to(cwd)}")
                
            except Exception as e:
                pass  # Silently skip parsing errors
        
        # STEP 4: Find models (check both singular and plural)
        model_patterns = [
            cwd / 'Models' / f"{singular}.cs",
            cwd / 'Models' / f"{plural}.cs",
            cwd / 'Models' / f"{singular}Model.cs",
            cwd / 'Models' / f"{plural}Model.cs",
            cwd / 'Models' / f"{singular.capitalize()}.cs",
            cwd / 'Models' / f"{plural.capitalize()}.cs",
        ]
        
        for model_path in model_patterns:
            if model_path.exists():
                if not should_exclude_file(model_path, exclude_patterns):
                    feature_files.add(model_path)
                    print(f"     Found model: {model_path.relative_to(cwd)}")
        
        # Search Models folder by pattern (both singular and plural)
        for search_term in [singular, plural]:
            for model_file in cwd.rglob(f"*{search_term}*.cs"):
                if 'Models' in model_file.parts:
                    if any(ex in model_file.parts for ex in EXCLUDE_DIRS):
                        continue
                    if not should_exclude_file(model_file, exclude_patterns):
                        feature_files.add(model_file)
        
        # STEP 5: Find DTOs/ViewModels (both singular and plural)
        for search_term in [singular, plural]:
            for dto_file in cwd.rglob(f"*{search_term}*Dto*.cs"):
                if any(ex in dto_file.parts for ex in EXCLUDE_DIRS):
                    continue
                if not should_exclude_file(dto_file, exclude_patterns):
                    feature_files.add(dto_file)
            
            for vm_file in cwd.rglob(f"*{search_term}*ViewModel*.cs"):
                if any(ex in vm_file.parts for ex in EXCLUDE_DIRS):
                    continue
                if not should_exclude_file(vm_file, exclude_patterns):
                    feature_files.add(vm_file)
    
    return feature_files

def resolve_folders(folder_names: list, exclude_patterns: list = None) -> set:
    """Find all files in specified folders."""
    cwd = Path.cwd()
    folder_files = set()
    exclude_patterns = exclude_patterns or []
    
    print(f"\n  📁 Searching folders: {', '.join(folder_names)}")
    
    for folder_name in folder_names:
        # Try multiple common locations for the folder
        possible_paths = [
            cwd / folder_name,
            cwd / folder_name.capitalize(),
            cwd / folder_name.upper(),
            cwd / folder_name.lower(),
        ]
        
        # Also search in common parent folders
        for parent in ['', 'Data', 'Models', 'Services', 'Controllers', 'Views']:
            possible_paths.append(cwd / parent / folder_name)
            possible_paths.append(cwd / parent / folder_name.capitalize())
        
        found_folder = False
        for folder_path in possible_paths:
            if folder_path.exists() and folder_path.is_dir():
                found_folder = True
                print(f"     Found folder: {folder_path.relative_to(cwd)}")
                
                # Recursively add all files in the folder
                for file in folder_path.rglob('*'):
                    if file.is_file():
                        if any(ex in file.parts for ex in EXCLUDE_DIRS):
                            continue
                        if should_exclude_file(file, exclude_patterns):
                            continue
                        folder_files.add(file)
        
        if not found_folder:
            print(f"     ⚠️ Folder '{folder_name}' not found in common locations")
            # Try fuzzy search
            for item in cwd.rglob(f"*{folder_name}*"):
                if item.is_dir() and not any(ex in item.parts for ex in EXCLUDE_DIRS):
                    print(f"     Found similar folder: {item.relative_to(cwd)}")
                    for file in item.rglob('*'):
                        if file.is_file():
                            if any(ex in file.parts for ex in EXCLUDE_DIRS):
                                continue
                            if should_exclude_file(file, exclude_patterns):
                                continue
                            folder_files.add(file)
    
    return folder_files

def open_in_chrome(filepath: str):
    """Open the exported file in Chrome."""
    try:
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
            print("⚠️ Chrome not found, opening with default browser...")
            os.startfile(filepath)
            
    except Exception as e:
        print(f"⚠️ Could not open file automatically: {e}")
        print(f"📁 File location: {os.path.abspath(filepath)}")

def export_project(output_file: str, feature_filter: list = None, folder_filter: list = None, 
                  exclude_patterns: list = None, no_open: bool = False):
    """Export project files to a single text file."""
    cwd = Path.cwd()
    files_exported = 0
    total_chars = 0
    exclude_patterns = exclude_patterns or []
    excluded_count = 0
    
    # Collect files
    if feature_filter or folder_filter:
        print()
        all_files = set()
        
        if feature_filter:
            feature_files = resolve_features(feature_filter, exclude_patterns)
            all_files.update(feature_files)
            print(f"\n🔍 Found {len(feature_files)} files for feature(s): {', '.join(feature_filter)}")
        
        if folder_filter:
            folder_files = resolve_folders(folder_filter, exclude_patterns)
            all_files.update(folder_files)
            print(f"\n📁 Found {len(folder_files)} files in folder(s): {', '.join(folder_filter)}")
        
        all_files = sorted(all_files)
        print(f"📊 Total files to export: {len(all_files)}")
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
    
    # Content buffer building
    content_buffer = []
    
    content_buffer.append(f"Project: {cwd}\n")
    content_buffer.append(f"{cwd.name} Export\n")
    content_buffer.append(f"Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")
    if feature_filter:
        content_buffer.append(f"Features: {', '.join(feature_filter)}\n")
    if folder_filter:
        content_buffer.append(f"Folders: {', '.join(folder_filter)}\n")
    content_buffer.append(f"Files: {len(all_files)}\n")
    content_buffer.append("Auto-excluded: .cshtml.cs files, wwwroot/lib/*\n")
    if exclude_patterns:
        content_buffer.append(f"User excluded: {', '.join(exclude_patterns)}\n")
    content_buffer.append("=" * 60 + "\n")
    
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
    
    content_buffer.append("\n" + "=" * 60 + "\n")
    content_buffer.append("📄 FILE CONTENTS\n")
    content_buffer.append("=" * 60 + "\n")
    
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
    
    total_chars += sum(len(s) for s in content_buffer)
    token_estimate = estimate_tokens(''.join(content_buffer + file_contents))
    
    token_info = [
        f"{'=' * 60}\n",
        f"📊 ESTIMATED TOKEN CONSUMPTION\n",
        f"{'=' * 60}\n",
        f"Characters: {total_chars:,}\n",
        f"Estimated Tokens: ~{token_estimate:,} (1 token ≈ 4 chars)\n",
        f"Files: {files_exported}\n",
        f"{'=' * 60}\n\n"
    ]
    
    with open(output_file, 'w', encoding='utf-8-sig') as out:
        out.write(''.join(token_info))
        out.write(''.join(content_buffer))
        out.write(''.join(file_contents))
    
    print(f"\n📊 Estimated tokens: ~{token_estimate:,}")
    print(f"✅ Exported {files_exported} files to {output_file}")
    
    if exclude_patterns:
        print(f"🚫 User excluded patterns: {', '.join(exclude_patterns)}")
    print(f"🚫 Auto-excluded: .cshtml.cs files, wwwroot/lib/*")
    
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

def list_folders():
    """List all detected folders in the project."""
    cwd = Path.cwd()
    folders = set()
    
    for item in cwd.rglob('*'):
        if item.is_dir() and not any(ex in item.parts for ex in EXCLUDE_DIRS):
            rel_path = item.relative_to(cwd)
            if str(rel_path) != '.':
                folders.add(str(rel_path))
    
    print("\n  Available Folders")
    print("  " + "-" * 50)
    for f in sorted(folders)[:50]:  # Show first 50 to avoid overwhelming output
        print(f"    -{f}")
    if len(folders) > 50:
        print(f"    ... and {len(folders) - 50} more")
    print()

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Export ThriftLoop project files')
    parser.add_argument('--output', '-o', default='Project_Export.txt', help='Output file name')
    parser.add_argument('--feature', '-f', nargs='+', help='Filter by feature name(s)')
    parser.add_argument('--folder', '-d', nargs='+', help='Include specific folder(s) like Migrations, Models')
    parser.add_argument('--exclude', '-e', nargs='+', help='Exclude files by extension (.css) or path keyword')
    parser.add_argument('--list-features', '-l', action='store_true', help='List all detected features')
    parser.add_argument('--list-folders', '-L', action='store_true', help='List all available folders')
    parser.add_argument('--no-open', '-n', action='store_true', help='Skip opening the output file in Chrome')
    
    args = parser.parse_args()
    
    if args.list_features:
        list_features()
    elif args.list_folders:
        list_folders()
    else:
        export_project(args.output, args.feature, args.folder, args.exclude, args.no_open)