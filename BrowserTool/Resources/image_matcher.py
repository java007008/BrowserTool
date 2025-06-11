#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
图像匹配脚本 - image_matcher.py
使用pyautogui在屏幕上查找指定图片并返回坐标
放在exe所在目录即可
"""

import sys
import os
import pyautogui
import time
import logging
from datetime import datetime

# 配置日志
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler('log/image_matcher.log', encoding='utf-8'),
        logging.StreamHandler()
    ]
)

logger = logging.getLogger(__name__)

def find_image_on_screen(image_path, confidence=0.8, timeout=10):
    """
    在屏幕上查找指定图片
    
    Args:
        image_path (str): 图片文件路径
        confidence (float): 匹配置信度 (0.0-1.0)
        timeout (int): 超时时间（秒）
    
    Returns:
        tuple: (x, y) 坐标，找不到返回 None
    """
    try:
        # 检查图片文件是否存在
        if not os.path.exists(image_path):
            logger.error(f"图片文件不存在: {image_path}")
            return None
        
        logger.info(f"开始查找图片: {image_path}")
        logger.info(f"置信度: {confidence}, 超时时间: {timeout}秒")
        
        start_time = time.time()
        
        while time.time() - start_time < timeout:
            try:
                # 使用pyautogui查找图片
                location = pyautogui.locateOnScreen(
                    image_path, 
                    confidence=confidence
                )
                
                if location is not None:
                    # 获取图片中心坐标
                    center_x, center_y = pyautogui.center(location)
                    logger.info(f"找到图片位置: ({center_x}, {center_y})")
                    logger.info(f"图片区域: {location}")
                    return (center_x, center_y)
                
            except pyautogui.ImageNotFoundException:
                # 图片未找到，继续等待
                pass
            except Exception as e:
                logger.warning(f"查找图片时发生异常: {e}")
            
            # 等待一段时间后再次尝试
            time.sleep(0.5)
        
        logger.warning(f"超时未找到图片: {image_path}")
        return None
        
    except Exception as e:
        logger.error(f"查找图片时发生严重异常: {e}")
        return None

def capture_screen_for_debug(save_path="debug_screenshot.png"):
    """
    截取当前屏幕用于调试
    
    Args:
        save_path (str): 截图保存路径
    """
    try:
        screenshot = pyautogui.screenshot()
        screenshot.save(save_path)
        logger.info(f"调试截图已保存: {save_path}")
    except Exception as e:
        logger.error(f"截图失败: {e}")

def check_pyautogui_environment():
    """
    检查pyautogui环境是否正常
    """
    try:
        # 获取屏幕分辨率
        screen_width, screen_height = pyautogui.size()
        logger.info(f"屏幕分辨率: {screen_width}x{screen_height}")
        
        # 检查pyautogui版本
        logger.info(f"PyAutoGUI版本: {pyautogui.__version__}")
        
        # 测试截图功能
        screenshot = pyautogui.screenshot()
        logger.info(f"截图测试成功，图片大小: {screenshot.size}")
        
        return True
    except Exception as e:
        logger.error(f"PyAutoGUI环境检查失败: {e}")
        return False

def main():
    """
    主函数
    """
    try:
        # 检查命令行参数
        if len(sys.argv) != 2:
            logger.error("用法: python image_matcher.py <图片路径>")
            print("用法: python image_matcher.py <图片路径>")
            sys.exit(1)
        
        image_path = sys.argv[1]
        logger.info(f"接收到图片路径: {image_path}")
        
        # 检查pyautogui环境
        if not check_pyautogui_environment():
            logger.error("PyAutoGUI环境检查失败")
            sys.exit(1)
        
        # 禁用pyautogui的安全特性（允许在屏幕边角移动鼠标）
        pyautogui.FAILSAFE = False
        
        # 设置pyautogui的暂停时间
        pyautogui.PAUSE = 0.1
        
        # 查找图片
        coordinates = find_image_on_screen(
            image_path, 
            confidence=0.8,  # 可以根据需要调整置信度
            timeout=10       # 查找超时时间
        )
        
        if coordinates is not None:
            x, y = coordinates
            # 输出坐标供C#程序读取（重要：这是C#程序读取的内容）
            print(f"{x},{y}")
            logger.info(f"成功找到图片，坐标: ({x}, {y})")
            sys.exit(0)  # 成功退出
        else:
            logger.error("未找到指定图片")
            # 保存调试截图
            debug_filename = f"debug_{datetime.now().strftime('%Y%m%d_%H%M%S')}.png"
            capture_screen_for_debug(debug_filename)
            logger.info(f"已保存调试截图: {debug_filename}")
            sys.exit(1)  # 失败退出
            
    except KeyboardInterrupt:
        logger.info("用户中断程序")
        sys.exit(1)
    except Exception as e:
        logger.error(f"程序执行异常: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()