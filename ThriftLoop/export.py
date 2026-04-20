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
INCLUDE_EXTS = {'.cs', '.cshtml', '.json', '.html', '.css', '.js', '.ts', '.tsx', '.jsx'}
ALWAYS_INCLUDE = {'Program.cs', 'ApplicationDbContext.cs', 'BaseController.cs', 
                  '_Layout.cshtml', '_Navbar.cshtml', '_ViewImports.cshtml', '_ViewStart.cshtml'}

# Related folders to search for feature components
RELATED_FOLDERS = {
    'Hubs': ['*Hub.cs', '*HubBase.cs'],
    'Services': ['*Service.cs', 'I*Service.cs'],
    'Repositories': ['*Repository.cs', 'I*Repository.cs'],
    'Models': ['*.cs'],
    'DTOs': ['*Dto.cs', '*DTO.cs', '*Request.cs', '*Response.cs'],
    'ViewModels': ['*ViewModel.cs', '*VM.cs'],
    'Views': ['*.cshtml'],
    'Components': ['*.cs', '*.razor'],
    'Pages': ['*.cshtml', '*.cshtml.cs'],
    'Controllers': ['*Controller.cs'],
    'Data': ['*.cs'],
    'Mappings': ['*Profile.cs', '*Mapping.cs'],
    'Validators': ['*Validator.cs'],
    'Middlewares': ['*Middleware.cs'],
    'Extensions': ['*Extensions.cs'],
    'Helpers': ['*Helper.cs', '*Utils.cs'],
    'Interfaces': ['I*.cs'],
    'Events': ['*Event.cs', '*EventHandler.cs'],
    'Commands': ['*Command.cs', '*CommandHandler.cs'],
    'Queries': ['*Query.cs', '*QueryHandler.cs'],
    'Notifications': ['*Notification.cs', '*NotificationHandler.cs'],
    'Clients': ['*Client.cs', 'I*Client.cs'],
    'Workers': ['*Worker.cs', '*Job.cs'],
}

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
    
    # Always exclude .cshtml.cs files (designer files) unless specifically included
    if file_path.name.endswith('.cshtml.cs'):
        # Check if it's a Razor Page code-behind (which we might want)
        if 'Pages' in file_path.parts:
            return False
        return True
    
    # Always exclude wwwroot/lib contents
    if '/wwwroot/lib/' in rel_path or 'wwwroot\\lib\\' in rel_path:
        return True
    
    # Exclude generated files
    if '.generated.cs' in file_path.name.lower() or '.designer.cs' in file_path.name.lower():
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
    if feature.endswith('s'):
        singular = feature[:-1]
        return singular
    return feature

def get_feature_variants(feature: str) -> list:
    """Get all possible naming variants for a feature."""
    singular = normalize_feature_name(feature)
    plural = singular + 's'
    
    variants = [
        feature,                    # Original (chat)
        singular,                   # Singular (chat)
        plural,                    # Plural (chats)
        feature.lower(),           # lowercase (chat)
        feature.upper(),           # UPPERCASE (CHAT)
        feature.capitalize(),      # Capitalized (Chat)
        singular.capitalize(),     # Singular capitalized (Chat)
        plural.capitalize(),       # Plural capitalized (Chats)
        feature.replace('_', ''),  # No underscores
        feature.replace('-', ''),  # No hyphens
    ]
    
    # Add camelCase and PascalCase variations
    words = re.findall(r'[A-Z]?[a-z]+|[A-Z]+(?=[A-Z]|$)', feature)
    if words:
        camel_case = words[0].lower() + ''.join(w.capitalize() for w in words[1:])
        pascal_case = ''.join(w.capitalize() for w in words)
        variants.extend([camel_case, pascal_case])
    
    return list(set(variants))  # Remove duplicates

def search_for_feature_files(feature: str, cwd: Path, exclude_patterns: list) -> set:
    """Comprehensive search for all files related to a feature."""
    feature_files = set()
    variants = get_feature_variants(feature)
    singular = normalize_feature_name(feature)
    
    print(f"     Searching with variants: {', '.join(variants[:5])}{'...' if len(variants) > 5 else ''}")
    
    # Search in all relevant folders
    for folder_name, file_patterns in RELATED_FOLDERS.items():
        folder_path = cwd / folder_name
        
        if folder_path.exists():
            for variant in variants:
                for pattern in file_patterns:
                    # Replace * with variant in pattern
                    search_pattern = pattern.replace('*', f'*{variant}*')
                    
                    for file in folder_path.rglob(search_pattern):
                        if any(ex in file.parts for ex in EXCLUDE_DIRS):
                            continue
                        if should_exclude_file(file, exclude_patterns):
                            continue
                        feature_files.add(file)
    
    # Search in Areas for feature-specific components
    areas_folder = cwd / 'Areas'
    if areas_folder.exists():
        for area in areas_folder.iterdir():
            if area.is_dir():
                # Search in each subfolder of the Area
                for subfolder in ['Controllers', 'Views', 'Models', 'Services', 'Hubs']:
                    area_subfolder = area / subfolder
                    if area_subfolder.exists():
                        for variant in variants:
                            search_pattern = f'*{variant}*'
                            for file in area_subfolder.rglob(search_pattern):
                                if file.is_file():
                                    if any(ex in file.parts for ex in EXCLUDE_DIRS):
                                        continue
                                    if should_exclude_file(file, exclude_patterns):
                                        continue
                                    feature_files.add(file)
    
    # Search for feature-specific views in Views folder
    views_folder = cwd / 'Views'
    if views_folder.exists():
        for variant in variants:
            # Look for view folders named after the feature
            for view_folder in views_folder.glob(f'*{variant}*'):
                if view_folder.is_dir():
                    for view_file in view_folder.rglob('*.cshtml'):
                        if not should_exclude_file(view_file, exclude_patterns):
                            feature_files.add(view_file)
            
            # Look for shared views that might contain the feature name
            shared_views = views_folder / 'Shared'
            if shared_views.exists():
                for view_file in shared_views.rglob(f'*{variant}*.cshtml'):
                    if not should_exclude_file(view_file, exclude_patterns):
                        feature_files.add(view_file)
    
    # Search for TypeScript/JavaScript files related to the feature
    wwwroot = cwd / 'wwwroot'
    if wwwroot.exists():
        js_folder = wwwroot / 'js'
        if js_folder.exists():
            for variant in variants:
                for ext in ['.js', '.ts', '.jsx', '.tsx']:
                    for js_file in js_folder.rglob(f'*{variant}*{ext}'):
                        if 'lib' not in js_file.parts:
                            if not should_exclude_file(js_file, exclude_patterns):
                                feature_files.add(js_file)
        
        # Check for feature-specific CSS
        css_folder = wwwroot / 'css'
        if css_folder.exists():
            for variant in variants:
                for css_file in css_folder.rglob(f'*{variant}*.css'):
                    if 'lib' not in css_file.parts:
                        if not should_exclude_file(css_file, exclude_patterns):
                            feature_files.add(css_file)
    
    return feature_files

def find_related_files_from_content(feature_files: set, cwd: Path, exclude_patterns: list) -> set:
    """Analyze already found files to discover more related files."""
    additional_files = set()
    variants = set()
    
    # Extract feature names from found files
    for file in feature_files:
        name_without_ext = file.stem
        # Remove common suffixes
        for suffix in ['Controller', 'Service', 'Repository', 'Hub', 'Model', 'Dto', 'ViewModel', 
                      'View', 'Component', 'Page', 'Handler', 'Validator', 'Middleware']:
            if name_without_ext.endswith(suffix):
                feature_name = name_without_ext[:-len(suffix)]
                variants.add(feature_name)
                variants.add(feature_name.lower())
                variants.add(feature_name.capitalize())
    
    # Parse file contents for references to other files
    for file in feature_files:
        if file.suffix == '.cs':
            try:
                content = file.read_text(encoding='utf-8')
                
                # Find interface implementations
                interfaces = re.findall(r':\s*(I[A-Z][a-zA-Z0-9]+)', content)
                for interface in interfaces:
                    impl_name = interface[1:]  # Remove 'I'
                    # Search for the implementation
                    for impl_file in cwd.rglob(f'*{impl_name}.cs'):
                        if any(ex in impl_file.parts for ex in EXCLUDE_DIRS):
                            continue
                        if not should_exclude_file(impl_file, exclude_patterns):
                            additional_files.add(impl_file)
                
                # Find constructor injections
                constructor_params = re.findall(r'\(\s*([^)]+)\s*\)', content)
                for params in constructor_params[:1]:  # Usually first one is constructor
                    for param in params.split(','):
                        param = param.strip()
                        if 'I' in param:
                            # Extract interface name
                            interface_match = re.search(r'I[A-Z][a-zA-Z0-9]+', param)
                            if interface_match:
                                interface_name = interface_match.group(0)
                                impl_name = interface_name[1:]
                                
                                # Search for the service/repository
                                for impl_file in cwd.rglob(f'*{impl_name}.cs'):
                                    if any(ex in impl_file.parts for ex in EXCLUDE_DIRS):
                                        continue
                                    if not should_exclude_file(impl_file, exclude_patterns):
                                        additional_files.add(impl_file)
                
                # Find DbSet references
                dbsets = re.findall(r'DbSet<([^>]+)>', content)
                for dbset in dbsets:
                    model_name = dbset.strip()
                    for model_file in cwd.rglob(f'*{model_name}.cs'):
                        if 'Models' in model_file.parts or 'Entities' in model_file.parts:
                            if not should_exclude_file(model_file, exclude_patterns):
                                additional_files.add(model_file)
                
                # Find using statements for related namespaces
                usings = re.findall(r'using\s+([^;]+);', content)
                for ns in usings:
                    if any(variant.lower() in ns.lower() for variant in variants):
                        # This namespace might contain related files
                        ns_parts = ns.split('.')
                        for i, part in enumerate(ns_parts):
                            if any(variant.lower() in part.lower() for variant in variants):
                                # Search for files in this namespace path
                                search_path = cwd.joinpath(*ns_parts[:i+1])
                                if search_path.exists():
                                    for file_in_ns in search_path.rglob('*.cs'):
                                        if not should_exclude_file(file_in_ns, exclude_patterns):
                                            additional_files.add(file_in_ns)
                
                # Find SignalR Hub references
                hub_references = re.findall(r'IHubContext<([^>]+)>', content)
                hub_references.extend(re.findall(r'HubConnection.*?url:\s*["\']([^"\']+)["\']', content))
                for hub_ref in hub_references:
                    hub_name = hub_ref.split('/')[-1].replace('Hub', '')
                    if hub_name:
                        for hub_file in cwd.rglob(f'*{hub_name}Hub.cs'):
                            if not should_exclude_file(hub_file, exclude_patterns):
                                additional_files.add(hub_file)
                
                # Find AutoMapper Profile references
                if 'CreateMap' in content:
                    map_types = re.findall(r'CreateMap<([^,>]+),\s*([^>]+)>', content)
                    for source, dest in map_types:
                        for type_name in [source.strip(), dest.strip()]:
                            for type_file in cwd.rglob(f'*{type_name}.cs'):
                                if not should_exclude_file(type_file, exclude_patterns):
                                    additional_files.add(type_file)
                
            except Exception:
                pass  # Silently skip parsing errors
        
        elif file.suffix in ['.cshtml', '.html']:
            try:
                content = file.read_text(encoding='utf-8')
                
                # Find JavaScript references
                script_srcs = re.findall(r'src=["\']([^"\']+)["\']', content)
                for src in script_srcs:
                    if not src.startswith(('http://', 'https://', '//')):
                        js_file = wwwroot / src.lstrip('/')
                        if js_file.exists() and not should_exclude_file(js_file, exclude_patterns):
                            additional_files.add(js_file)
                
                # Find CSS references
                link_hrefs = re.findall(r'href=["\']([^"\']+\.css)["\']', content)
                for href in link_hrefs:
                    if not href.startswith(('http://', 'https://', '//')):
                        css_file = wwwroot / href.lstrip('/')
                        if css_file.exists() and not should_exclude_file(css_file, exclude_patterns):
                            additional_files.add(css_file)
                
            except Exception:
                pass
    
    return additional_files

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
        
        # Step 1: Comprehensive search for feature files
        found_files = search_for_feature_files(feature, cwd, exclude_patterns)
        
        if found_files:
            print(f"     Found {len(found_files)} files directly related to '{feature}'")
            for file in sorted(found_files):
                print(f"       • {file.relative_to(cwd)}")
            feature_files.update(found_files)
        else:
            print(f"     ⚠️ No files found directly for feature '{feature}'")
        
        # Step 2: Find related files by analyzing content of found files
        print(f"     🔗 Analyzing dependencies...")
        related_files = find_related_files_from_content(feature_files, cwd, exclude_patterns)
        
        new_files = related_files - feature_files
        if new_files:
            print(f"     Found {len(new_files)} additional dependent files")
            for file in sorted(new_files):
                print(f"       • {file.relative_to(cwd)}")
            feature_files.update(related_files)
        
        # Step 3: Look for view imports and shared layouts that might be relevant
        views_folder = cwd / 'Views'
        if views_folder.exists():
            shared_folder = views_folder / 'Shared'
            if shared_folder.exists():
                # Check if any found views use specific layouts
                for file in feature_files:
                    if file.suffix == '.cshtml':
                        try:
                            content = file.read_text(encoding='utf-8')
                            layout_match = re.search(r'Layout\s*=\s*["\']([^"\']+)["\']', content)
                            if layout_match:
                                layout_path = layout_match.group(1)
                                if layout_path.startswith('~/'):
                                    layout_path = layout_path[2:]
                                elif layout_path.startswith('/'):
                                    layout_path = layout_path[1:]
                                
                                layout_file = cwd / layout_path
                                if not layout_file.exists():
                                    # Try in Views/Shared
                                    layout_name = Path(layout_path).name
                                    layout_file = shared_folder / layout_name
                                
                                if layout_file.exists() and not should_exclude_file(layout_file, exclude_patterns):
                                    feature_files.add(layout_file)
                        except Exception:
                            pass
    
    # If no files found at all, provide helpful message
    if len(feature_files) <= len(ALWAYS_INCLUDE):
        print(f"\n  💡 Tip: Try using --list-features to see available feature names")
        print(f"  💡 Tip: Use --folder to include entire folders like 'Hubs', 'Models', etc.")
    
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
        for parent in ['', 'Data', 'Models', 'Services', 'Controllers', 'Views', 'Areas', 'Hubs']:
            possible_paths.append(cwd / parent / folder_name)
            possible_paths.append(cwd / parent / folder_name.capitalize())
        
        # Check Areas subfolders
        areas_folder = cwd / 'Areas'
        if areas_folder.exists():
            for area in areas_folder.iterdir():
                if area.is_dir():
                    possible_paths.append(area / folder_name)
                    possible_paths.append(area / folder_name.capitalize())
        
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
            found_similar = False
            for item in cwd.rglob(f"*{folder_name}*"):
                if item.is_dir() and not any(ex in item.parts for ex in EXCLUDE_DIRS):
                    print(f"     Found similar folder: {item.relative_to(cwd)}")
                    found_similar = True
                    for file in item.rglob('*'):
                        if file.is_file():
                            if any(ex in file.parts for ex in EXCLUDE_DIRS):
                                continue
                            if should_exclude_file(file, exclude_patterns):
                                continue
                            folder_files.add(file)
            
            if not found_similar:
                print(f"     💡 No folders found matching '{folder_name}'")
    
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
        if excluded_count > 0:
            print(f"🚫 Excluded {excluded_count} files based on patterns")
    
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
    content_buffer.append("Auto-excluded: .cshtml.cs files, wwwroot/lib/*, generated files\n")
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
    print(f"🚫 Auto-excluded: .cshtml.cs files, wwwroot/lib/*, generated files")
    
    if not no_open:
        print("🌐 Opening in Chrome...")
        open_in_chrome(output_file)

def list_features():
    """List all detected features from Controllers, Hubs, Services, etc."""
    cwd = Path.cwd()
    features = set()
    
    # Check Controllers
    for file in cwd.rglob('*Controller.cs'):
        if any(ex in file.parts for ex in EXCLUDE_DIRS):
            continue
        if file.stem not in ['BaseController', 'HomeController', 'AccountController']:
            features.add(file.stem.replace('Controller', ''))
    
    # Check Hubs
    for file in cwd.rglob('*Hub.cs'):
        if any(ex in file.parts for ex in EXCLUDE_DIRS):
            continue
        features.add(file.stem.replace('Hub', ''))
    
    # Check Services
    for file in cwd.rglob('*Service.cs'):
        if any(ex in file.parts for ex in EXCLUDE_DIRS):
            continue
        if not file.stem.startswith('I'):
            service_name = file.stem.replace('Service', '')
            if service_name and not service_name.endswith('Base'):
                features.add(service_name)
    
    # Check View folders
    views_folder = cwd / 'Views'
    if views_folder.exists():
        for item in views_folder.iterdir():
            if item.is_dir() and item.name not in ['Shared', 'Home', 'Account']:
                features.add(item.name)
    
    # Check Areas
    areas_folder = cwd / 'Areas'
    if areas_folder.exists():
        for area in areas_folder.iterdir():
            if area.is_dir():
                area_views = area / 'Views'
                if area_views.exists():
                    for item in area_views.iterdir():
                        if item.is_dir() and item.name not in ['Shared']:
                            features.add(f"{area.name}.{item.name}")
    
    print("\n  Detected Features")
    print("  " + "-" * 50)
    
    if features:
        for f in sorted(features):
            print(f"    --feature {f}")
    else:
        print("    No features detected")
    
    print(f"\n  Total: {len(features)} features found")
    print()

def list_folders():
    """List all detected folders in the project."""
    cwd = Path.cwd()
    folders = set()
    
    # List main project folders
    important_folders = ['Controllers', 'Views', 'Models', 'Services', 'Hubs', 'Data', 
                        'DTOs', 'ViewModels', 'Repositories', 'Migrations', 'Components',
                        'Pages', 'wwwroot', 'Areas', 'Extensions', 'Middlewares']
    
    print("\n  Common Project Folders")
    print("  " + "-" * 50)
    
    for folder in important_folders:
        if (cwd / folder).exists():
            print(f"    --folder {folder}")
    
    # List Areas
    areas_folder = cwd / 'Areas'
    if areas_folder.exists():
        print("\n  Areas")
        print("  " + "-" * 50)
        for area in areas_folder.iterdir():
            if area.is_dir():
                print(f"    --folder Areas/{area.name}")
    
    # List other folders
    print("\n  Other Folders (first 30)")
    print("  " + "-" * 50)
    count = 0
    for item in cwd.rglob('*'):
        if item.is_dir() and not any(ex in item.parts for ex in EXCLUDE_DIRS):
            rel_path = item.relative_to(cwd)
            if str(rel_path) != '.' and not any(important in str(rel_path) for important in important_folders):
                folders.add(str(rel_path))
    
    for f in sorted(folders)[:30]:
        print(f"    --folder {f}")
        count += 1
    
    if len(folders) > 30:
        print(f"    ... and {len(folders) - 30} more")
    
    print()

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Export ThriftLoop project files')
    parser.add_argument('--output', '-o', default='Project_Export.txt', help='Output file name')
    parser.add_argument('--feature', '-f', nargs='+', help='Filter by feature name(s)')
    parser.add_argument('--folder', '-d', nargs='+', help='Include specific folder(s) like Migrations, Models, Hubs')
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